using ArxLibertatisEditorIO.RawIO.FTL;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using SharpGLTF.Geometry;
using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.Materials;
using SharpGLTF.Schema2;

namespace ArxLibertatisFTLConverter
{
    class ConvertFTLtoGLTF2
    {
        private class OrderedMeshData
        {
            public string name;
            public List<Vector3> verts = new List<Vector3>();
            public List<Vector3> normals = new List<Vector3>();
            public List<Vector2> UVs = new List<Vector2>();
        }
        public static void Convert(string file)
        {

            bool debug = true;
            Console.WriteLine("Attempting to convert " + file);

            //setup directory related info

            string parentDir = Path.GetDirectoryName(file);
            string fileName = Path.GetFileNameWithoutExtension(file);
            string outputDir = Path.Combine(parentDir, fileName + "_FTLToGLTF");
            string outputName = Path.Combine(outputDir, fileName + ".gltf");
            string gameDir = Util.GetParentWithName(parentDir, "Game");

            if (debug)
            {
                Console.WriteLine("###Debug Pathinfo###");
                Console.Write("Parent Directory: ");
                Console.Write(parentDir);
                Console.WriteLine("");
                Console.Write("Output Directory: ");
                Console.Write(outputDir);
                Console.WriteLine("");
                Console.Write("Game Directory: ");
                Console.Write(gameDir);
                Console.WriteLine("");
                Console.Write("Output Name: ");
                Console.Write(outputDir);
                Console.WriteLine("");
                Console.WriteLine("###END###");

            }

            if (gameDir == null)
            {
                Console.WriteLine("Could not find game directory!");
                return;
            }


            Directory.CreateDirectory(outputDir);


            //Leverage FTL IO 


            FTL_IO fTL_IO = new FTL_IO();


            using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                var s = FTL_IO.EnsureUnpacked(fs);
                fTL_IO.ReadFrom(s);
            }


            if (debug)
            {
                Console.WriteLine("###Debug FTL_IO Info###");
                Console.Write("Vertex Amount: ");
                Console.Write(fTL_IO._3DDataSection.vertexList.Length);
                Console.WriteLine("");
                Console.Write("Face Amount : ");
                Console.Write(fTL_IO._3DDataSection.faceList.Length);
                Console.WriteLine("");
                Console.WriteLine("###END###");
            }


            //generate lists of ordered vertex data
            //TODO: Figure out of tripling the number of verts is an issue or not
            //TODO: Materials

            OrderedMeshData orderedMeshData = new OrderedMeshData();

            orderedMeshData.name = fileName;


            for (int i = 0; i < fTL_IO._3DDataSection.faceList.Length; ++i)
            {
                var face = fTL_IO._3DDataSection.faceList[i];

                for (int j = 0; j < face.vid.Length; j++)
                {
                    //select vertex from face vertex index list
                    var vert = fTL_IO._3DDataSection.vertexList[face.vid[j]];
                    //append to ordered mesh verts
                    orderedMeshData.verts.Add(new Vector3(vert.vert.x, vert.vert.y, vert.vert.z));

                    // add Uvs in order
                    orderedMeshData.UVs.Add(new Vector2(face.u[0], face.v[0]));
                    orderedMeshData.UVs.Add(new Vector2(face.u[1], face.v[1]));
                    orderedMeshData.UVs.Add(new Vector2(face.u[2], face.v[2]));

                    orderedMeshData.normals.Add(new Vector3(vert.norm.x, vert.norm.y, vert.norm.z));

                }

            }

            if (debug)
            {
                Console.WriteLine("###Debug Dump Ordered Mesh Data###");
                Console.WriteLine(orderedMeshData.name);
                Console.Write("Verts Amount: ");
                Console.Write(orderedMeshData.verts.Count);
                Console.WriteLine("");
                Console.Write("Normals Amount : ");
                Console.Write(orderedMeshData.normals.Count);
                Console.WriteLine("");
                Console.Write("UV coord Amount : ");
                Console.Write(orderedMeshData.UVs.Count);
                Console.WriteLine("");
                Console.WriteLine("###END###");
            }

            // GLTF danger zone / overly verbose shitbin from here onwards

            //TODO: split mesh per group
            var sceneMesh = new MeshBuilder<VertexPositionNormal, VertexTexture1>(fileName);

            var material = new MaterialBuilder()
                .WithDoubleSide(true);

            var primitives = sceneMesh.UsePrimitive(material, 3);

            for (int i = 0; i < orderedMeshData.verts.Count; i += 3)
            {
                VertexPositionNormal v1 = new VertexPositionNormal(orderedMeshData.verts[i], orderedMeshData.normals[i]);
                VertexPositionNormal v2 = new VertexPositionNormal(orderedMeshData.verts[i + 1], orderedMeshData.normals[i + 1]);
                VertexPositionNormal v3 = new VertexPositionNormal(orderedMeshData.verts[i + 2], orderedMeshData.normals[i + 2]);

                VertexTexture1 vt1 = new VertexTexture1(orderedMeshData.UVs[i]);
                VertexTexture1 vt2 = new VertexTexture1(orderedMeshData.UVs[i + 1]);
                VertexTexture1 vt3 = new VertexTexture1(orderedMeshData.UVs[i + 2]);

                VertexBuilder<VertexPositionNormal, VertexTexture1, VertexEmpty> ver1 = new VertexBuilder<VertexPositionNormal, VertexTexture1, VertexEmpty>(v1, vt1);
                VertexBuilder<VertexPositionNormal, VertexTexture1, VertexEmpty> ver2 = new VertexBuilder<VertexPositionNormal, VertexTexture1, VertexEmpty>(v2, vt2);
                VertexBuilder<VertexPositionNormal, VertexTexture1, VertexEmpty> ver3 = new VertexBuilder<VertexPositionNormal, VertexTexture1, VertexEmpty>(v3, vt3);

                primitives.AddTriangle(ver1, ver2, ver3);
            }

            var scene = new SharpGLTF.Scenes.SceneBuilder();
            scene.AddRigidMesh(sceneMesh, Matrix4x4.Identity);

            var model = scene.ToGltf2();

            model.SaveGLTF(outputName);





            
        }
    }
}
