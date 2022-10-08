using ArxLibertatisEditorIO.RawIO.FTL;
using ArxLibertatisEditorIO.Util;
//using CSWavefront.Raw;
//Contains the autodictionary class?
using CSWavefront.Util;
using SharpGLTF.Geometry;
using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.Materials;
using SixLabors.ImageSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;



//TODO: Rewrite the entirety of this using the face group loop



namespace ArxLibertatisFTLConverter
{
    using VERTEX = SharpGLTF.Geometry.VertexTypes.VertexPositionNormal;

    internal class ConvertFTLtoGLTF
    {
        private class Material
        {
            public string name;
            public string textureFile;
        }



        public static void Convert(string file)
        {

            Console.WriteLine("Attempting to convert: " + file);
            string parentDir = Path.GetDirectoryName(file);
            string fileName = Path.GetFileNameWithoutExtension(file);
            string outputDir = Path.Combine(parentDir, fileName + "_FTLToGLTF");
            Directory.CreateDirectory(outputDir);
            string outputName = Path.Combine(outputDir, fileName + ".gltf");
            string gameDir = Util.GetParentWithName(parentDir, "Game");
            if (gameDir != null)
            {
                Settings.dataDir = Path.GetDirectoryName(gameDir);
            }
            else
            {
                Console.WriteLine("could not find game dir");
            }

            FTL_IO ftl = new FTL_IO();


            using (FileStream fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                Stream s = FTL_IO.EnsureUnpacked(fs);
                ftl.ReadFrom(s);
            }


            Vector4[] baseVerts = new Vector4[ftl._3DDataSection.vertexList.Length];
            Vector3[] baseNorms = new Vector3[baseVerts.Length];
            Material[] materials = new Material[ftl._3DDataSection.textureContainers.Length];

            //load base vertex data
            for (int i = 0; i < baseVerts.Length; ++i)
            {
                EERIE_OLD_VERTEX vert = ftl._3DDataSection.vertexList[i];
                baseVerts[i] = new Vector4(vert.vert.x, -vert.vert.y, vert.vert.z, 1);
                baseNorms[i] = new Vector3(vert.norm.x, -vert.norm.y, vert.norm.z);
            }

            //load materials
            for (int i = 0; i < materials.Length; ++i)
            {
                string texConName = IOHelper.GetString(ftl._3DDataSection.textureContainers[i].name);
                if (Settings.dataDir != null)
                {
                    string tmpName = Path.Combine(Settings.dataDir, Path.GetDirectoryName(texConName), Path.GetFileNameWithoutExtension(texConName));
                    if (File.Exists(tmpName + ".jpg"))
                    {
                        texConName = tmpName + ".jpg";
                    }
                    else if (File.Exists(tmpName + ".bmp"))
                    {
                        texConName = tmpName + ".bmp";
                    }

                    if (File.Exists(texConName))
                    {
                        File.Copy(texConName, Path.Combine(outputDir, Path.GetFileName(texConName)), true);
                    }
                    else
                    {
                        File.WriteAllText(Path.Combine(outputDir, Path.GetFileName(texConName)), "could not find texture");
                    }
                }

                Material mat = new Material { name = Path.GetFileNameWithoutExtension(texConName), textureFile = texConName };
                materials[i] = mat;
            }

            AutoDictionary<int, HashSet<string>> indexToGroup = new AutoDictionary<int, HashSet<string>>((x) => { return new HashSet<string>(); });

            //groups
            for (int i = 0; i < ftl._3DDataSection.groups.Length; ++i)
            {
                FTL_IO_3D_DATA_GROUP g = ftl._3DDataSection.groups[i];
                string name = IOHelper.GetStringSafe(g.group.name).Replace(' ', '_');
                for (int j = 0; j < g.indices.Length; j++)
                {
                    indexToGroup[g.indices[j]].Add(name);
                }
            }

            // ODD CONVERSION, 'W' ELEMENT OF VEC4 ARRAY APEARS MEANINGLESS ?
            Vector3[] vertsVec3 = new Vector3[baseVerts.Length];
            for (int i = 0; i < baseVerts.Length; i++)
            {
                vertsVec3[i].X = baseVerts[i].X;
                vertsVec3[i].Y = baseVerts[i].Y;
                vertsVec3[i].Z = baseVerts[i].Z;
            }

            // various lists of various shit that is hopefully ordered correctly according to faceList vid
            List<Vector3> orderedVertex = new List<Vector3>();
            List<Vector3> orderedVertexNorms = new List<Vector3>();
            List<Vector3> orderedU = new List<Vector3>();
            List<Vector3> orderedV = new List<Vector3>();





            for (int i = 0; i < ftl._3DDataSection.faceList.Length; ++i)
            {
                EERIE_FACE_FTL face = ftl._3DDataSection.faceList[i];
                PolyType faceType = face.facetype;
                uint[] rgb = face.rgb;
                ushort[] vid = face.vid;
                short texid = face.texid;
                float[] u = face.u;
                float[] v = face.v;
                short[] ou = face.ou;
                short[] ov = face.ov;
                float transval = face.transval;
                ArxLibertatisEditorIO.RawIO.Shared.SavedVec3 norm = face.norm;
                ArxLibertatisEditorIO.RawIO.Shared.SavedVec3[] normals = face.nrmls;
                float temp = face.temp;


                //select from vertsVec3, according to order in 'vid', append to triangles list
                for (int j = 0; j < vid.Length; j++)
                {
                    orderedVertex.Add(vertsVec3[vid[j]]);

                    orderedVertexNorms.Add(baseNorms[vid[j]]);



                }


                // order from ov, ou, I didn't do this but it seems to work anyway
                orderedU.Add(new Vector3(u[0], u[1], u[2]));
                orderedV.Add(new Vector3(v[0], v[1], v[2]));



            }



            MeshBuilder<VERTEX, VertexTexture1> mesh = new MeshBuilder<VERTEX, VertexTexture1>(fileName);

            // I'm pretty sure this makes a duplicate mesh for every material, oh well
            for (int textureIndex = 0; textureIndex != materials.Length; textureIndex++)
            {
                string texturepath = materials[textureIndex].textureFile;


                //THIS POS gltf doesn't accept BMP so we gotta convert it to PNG first
                using (SixLabors.ImageSharp.Image image = SixLabors.ImageSharp.Image.Load(texturepath))
                {
                    image.SaveAsPng(texturepath);
                }

                // Then assign the basecolor to the new png file we dumped

                MaterialBuilder material1 = new MaterialBuilder()
                    .WithBaseColor(texturepath)
                    .WithDoubleSide(true)
                    .WithMetallicRoughnessShader()
                    .WithAlpha();


                //1 - points, 2 - lines, 3 - tris
                PrimitiveBuilder<MaterialBuilder, VERTEX, VertexTexture1, VertexEmpty> prim = mesh.UsePrimitive(material1, 3);

                // base mesh

                for (int i = 0; i < orderedVertex.Count; i += 3)
                {
                    //Vertice Positions
                    Vector3 position1 = new Vector3(orderedVertex[i].X, orderedVertex[i].Y, orderedVertex[i].Z);
                    Vector3 position2 = new Vector3(orderedVertex[i + 1].X, orderedVertex[i + 1].Y, orderedVertex[i + 1].Z);
                    Vector3 position3 = new Vector3(orderedVertex[i + 2].X, orderedVertex[i + 2].Y, orderedVertex[i + 2].Z);

                    // create VERTEX with position and normal (SharpGLTF.Geometry.VertexTypes.VertexPositionNormal;)
                    // arx format supplies vertex normals and face normals, no idea how to get GLTF to accept both in a meaningful way
                    VERTEX v1 = new VERTEX(position1, orderedVertexNorms[i]);
                    VERTEX v2 = new VERTEX(position2, orderedVertexNorms[i + 1]);
                    VERTEX v3 = new VERTEX(position3, orderedVertexNorms[i + 2]);

                    //UV coords
                    // XYZ here is meaningless, just accessors 
                    VertexTexture1 cor1 = new VertexTexture1(new Vector2(orderedU[i / 3].X, orderedV[i / 3].X));
                    VertexTexture1 cor2 = new VertexTexture1(new Vector2(orderedU[i / 3].Y, orderedV[i / 3].Y));
                    VertexTexture1 cor3 = new VertexTexture1(new Vector2(orderedU[i / 3].Z, orderedV[i / 3].Z));

                    //Final vertex structure

                    //this is extremely verbose, jesus
                    VertexBuilder<VertexPositionNormal, VertexTexture1, VertexEmpty> ver1 = new SharpGLTF.Geometry.VertexBuilder<VertexPositionNormal, VertexTexture1, VertexEmpty>(v1, cor1);
                    VertexBuilder<VertexPositionNormal, VertexTexture1, VertexEmpty> ver2 = new SharpGLTF.Geometry.VertexBuilder<VertexPositionNormal, VertexTexture1, VertexEmpty>(v2, cor2);
                    VertexBuilder<VertexPositionNormal, VertexTexture1, VertexEmpty> ver3 = new SharpGLTF.Geometry.VertexBuilder<VertexPositionNormal, VertexTexture1, VertexEmpty>(v3, cor3);

                    prim.AddTriangle(ver1, ver2, ver3);
                }
            }


            SharpGLTF.Scenes.SceneBuilder scene = new SharpGLTF.Scenes.SceneBuilder();

            scene.AddRigidMesh(mesh, Matrix4x4.Identity);

            SharpGLTF.Schema2.ModelRoot model = scene.ToGltf2();

            model.SaveGLTF(outputName);
        }
    }
}
