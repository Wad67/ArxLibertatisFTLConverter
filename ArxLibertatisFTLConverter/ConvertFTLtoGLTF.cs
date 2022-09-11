using ArxLibertatisEditorIO.RawIO.FTL;
using ArxLibertatisEditorIO.Util;
//using CSWavefront.Raw;
//Contains the autodictionary class?
using CSWavefront.Util;
using SharpGLTF.Geometry;
using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.Materials;
using SharpGLTF.Schema2;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;



namespace ArxLibertatisFTLConverter
{
    using VERTEX = SharpGLTF.Geometry.VertexTypes.VertexPosition;
    class ConvertFTLtoGLTF
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
            string gameDir = Util.GetParentWithName(parentDir, "game");
            if (gameDir != null)
            {
                Settings.dataDir = Path.GetDirectoryName(gameDir);
            }
            else
            {
                Console.WriteLine("could not find game dir");
            }

            FTL_IO ftl = new FTL_IO();




            using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                var s = FTL_IO.EnsureUnpacked(fs);
                ftl.ReadFrom(s);
            }
            

            Vector4[] baseVerts = new Vector4[ftl._3DDataSection.vertexList.Length];
            Vector3[] baseNorms = new Vector3[baseVerts.Length];
            Material[] materials = new Material[ftl._3DDataSection.textureContainers.Length];

            //load base vertex data
            for (int i = 0; i < baseVerts.Length; ++i)
            {
                var vert = ftl._3DDataSection.vertexList[i];
                baseVerts[i] = new Vector4(vert.vert.x, -vert.vert.y, vert.vert.z, 1);
                baseNorms[i] = new Vector3(vert.norm.x, -vert.norm.y, vert.norm.z);
            }

            //load materials
            for (int i = 0; i < materials.Length; ++i)
            {
                var texConName = IOHelper.GetString(ftl._3DDataSection.textureContainers[i].name);
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
                var g = ftl._3DDataSection.groups[i];
                var name = IOHelper.GetStringSafe(g.group.name).Replace(' ', '_');
                for (int j = 0; j < g.indices.Length; j++)
                {
                    indexToGroup[g.indices[j]].Add(name);
                }
            }


            for (int i = 0; i < ftl._3DDataSection.faceList.Length; ++i)
            {
                var face = ftl._3DDataSection.faceList[i];
                HashSet<string> groups = new HashSet<string>();
                string materialName = "noMaterial";
                if (face.texid >= 0)
                {
                    materialName = materials[face.texid].name;
                }

            }

            var material1 = new MaterialBuilder()
                .WithDoubleSide(true)
                .WithMetallicRoughnessShader();
            //.WithChannelParam(KnownChannel.BaseColor, KnownProperty.RGBA, new Vector4(1, 0, 0, 1));

            var mesh = new MeshBuilder<VERTEX>("ArxMesh");

            var prim = mesh.UsePrimitive(material1);


            // Build up gltf primitives 
 
            // There is probably a very simple way of doing this, I just don't know how
            Vector3[] vertsVec3 = new Vector3[baseVerts.Length];
            for (int i = 0; i < baseVerts.Length ; i++)
            {
                vertsVec3[i].X = baseVerts[i].X;
                vertsVec3[i].Y = baseVerts[i].Y;
                vertsVec3[i].Z = baseVerts[i].Z;
            }

            // this is getting cranky!
            for (int i = 0; i < vertsVec3.Length; i++)
            {
                prim.AddPoint(new VERTEX(vertsVec3[i]));
            }
            
            var scene = new SharpGLTF.Scenes.SceneBuilder();

            scene.AddRigidMesh(mesh, Matrix4x4.Identity);

            var model = scene.ToGltf2();

            model.SaveGLTF(outputName);
        }
    }
}
