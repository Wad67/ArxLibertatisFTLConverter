﻿using ArxLibertatisEditorIO.RawIO.FTL;
using ArxLibertatisEditorIO.Util;
using ArxLibertatisEditorIO.RawIO.TEA;
using SharpGLTF.Geometry;
using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.Materials;
using SixLabors.ImageSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Linq;
using SixLabors.ImageSharp.Processing;

namespace ArxLibertatisFTLConverter
{
    internal class ConvertFTLtoGLTF2
    {
        private class OrderedMeshData
        {
            public string name;
            public List<Vector3> verts = new List<Vector3>();
            public List<Vector3> normals = new List<Vector3>();
            //vec 3 for per triangle
            public List<Vector3> U = new List<Vector3>();
            public List<Vector3> V = new List<Vector3>();
            public List<int> textureID = new List<int>();
        }

        private class AnimationData
        {
            public string name;
            public THEA_HEADER tea_header = new THEA_HEADER();
            public List<TEA_KEYFRAME> keyframes = new List<TEA_KEYFRAME>();

        }

        private class Material
        {
            public string name;
            public string textureFile;
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
            string animDir = gameDir + "\\graph\\obj3d\\anims\\npc";

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

            using (FileStream fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                Stream s = FTL_IO.EnsureUnpacked(fs);
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
            //generate material list

            Material[] materials = new Material[fTL_IO._3DDataSection.textureContainers.Length];

            for (int i = 0; i < materials.Length; ++i)
            {
                string texConName = IOHelper.GetString(fTL_IO._3DDataSection.textureContainers[i].name);

                string tmpName = Path.Combine(gameDir, Path.GetDirectoryName(texConName), Path.GetFileNameWithoutExtension(texConName));
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

                Material mat = new Material
                {
                    name = Path.GetFileNameWithoutExtension(texConName),
                    textureFile = Path.Combine(outputDir, Path.GetFileName(texConName))
                };
                materials[i] = mat;
                //THIS POS gltf doesn't accept BMP so we gotta convert it to PNG first
                //TODO: Convert (0,0,0) to alpha ?
                using SixLabors.ImageSharp.Image image = SixLabors.ImageSharp.Image.Load(mat.textureFile);
                image.Mutate(c => c.ProcessPixelRowsAsVector4(row =>
                {
                    for (int x = 0; x < row.Length; x++)
                    {
                        // convert 0,0,0,1 to 0,0,0,0
                        if (row[x] == new Vector4(0, 0, 0, 1))
                        {
                            row[x] = new Vector4(0, 0, 0, 0);
                        }

                    }
                }));
                image.SaveAsPng(mat.textureFile);
                

            }

            if (debug)
            {

                Console.WriteLine("###Debug Texture Info###");
                Console.Write("Texture Amount: ");
                Console.Write(materials.Length);
                Console.WriteLine("");
                for (int i = 0; i != materials.Length; i++)
                {
                    Console.Write(materials[i].name + " : ");
                    Console.Write(materials[i].textureFile + " \n");
                }
                Console.WriteLine("###END###");

            }

            //generate lists of ordered vertex data
            //TODO: Figure out of tripling the number of verts is an issue or not

            OrderedMeshData orderedMeshData = new OrderedMeshData
            {
                name = fileName
            };

            for (int i = 0; i < fTL_IO._3DDataSection.faceList.Length; ++i)
            {
                EERIE_FACE_FTL face = fTL_IO._3DDataSection.faceList[i];

                for (int j = 0; j < face.vid.Length; j++)
                {
                    //select vertex from face vertex index list
                    EERIE_OLD_VERTEX vert = fTL_IO._3DDataSection.vertexList[face.vid[j]];
                    //append to ordered mesh verts
                    orderedMeshData.verts.Add(new Vector3(vert.vert.x, vert.vert.y, vert.vert.z));
                    orderedMeshData.normals.Add(new Vector3(vert.norm.x, vert.norm.y, vert.norm.z));

                }

                //apparently UV's are ordered properly to begin with

                orderedMeshData.U.Add(new Vector3(face.u[0], face.u[1], face.u[2]));
                orderedMeshData.V.Add(new Vector3(face.v[0], face.v[1], face.v[2]));

                orderedMeshData.textureID.Add(face.texid);

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
                Console.Write(orderedMeshData.U.Count + orderedMeshData.V.Count);
                Console.WriteLine("");
                Console.Write("Face TexID's present : ");
                Console.Write(string.Join(", ", orderedMeshData.textureID.Distinct().ToList()));
                Console.WriteLine("");
                Console.WriteLine("###END###");
            }

            //Animation Handling Hell (AHH)
            TEA_IO tEA_IO = new TEA_IO();
            List<string> animationFiles = new List<string>();
            List<AnimationData> animationDataList = new List<AnimationData>();
            animationFiles.AddRange(Directory.GetFiles(animDir));

            foreach(string path in animationFiles)
            {
                string[] fileNamePieces = fileName.Split('_');
                //usually the tea files contain the first few characters of the FTL file, likely needs something better
                if (path.Contains(fileNamePieces[0]))
                {
                    Console.WriteLine(path);
                    AnimationData animationDataItem = new AnimationData();
                    animationDataItem.name = Path.GetFileName(path); //IOHelper.GetString(tEA_IO.header.identity); // Gibberish
                    TEA_KEYFRAME tEA_KEYFRAME = new TEA_KEYFRAME(tEA_IO);
                    using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        Stream s = FTL_IO.EnsureUnpacked(fs);

                        StructReader structReader = new StructReader(s);


                        tEA_IO.ReadFrom(s);

                        tEA_KEYFRAME.ReadFrom(structReader);
                    }
                    
                    MemoryStream stream = new MemoryStream();
                    tEA_IO.WriteTo(stream);

                    animationDataItem.tea_header = tEA_IO.header;
                    animationDataItem.keyframes.Add(tEA_KEYFRAME); 

                    animationDataList.Add(animationDataItem);

                }

            }










            // GLTF danger zone / overly verbose shitbin from here onwards

            MeshBuilder<VertexPositionNormal, VertexTexture1> sceneMesh = new MeshBuilder<VertexPositionNormal, VertexTexture1>(fileName);


            //Setup list of GLTF material slots
            List<MaterialBuilder> gltfMaterials = new List<MaterialBuilder>();
            for (int i = 0; i != materials.Length; i++)
            {
                string texturepath = materials[i].textureFile;

                MaterialBuilder newMaterial = new MaterialBuilder()
                    .WithBaseColor(texturepath)
                    .WithAlpha(AlphaMode.MASK)
                    .WithDoubleSide(true);
                    
                    
                gltfMaterials.Add(newMaterial);

            }

            //For multiple materials, have to define multiple primitive groups. 
            //Each primitive group coincides with each material slot
            //Some textureID's are -1, no idea what this means but I'm just going to subtract one from the count
            List<PrimitiveBuilder<MaterialBuilder, VertexPositionNormal, VertexTexture1, VertexEmpty>> gltfPrimitives = new List<PrimitiveBuilder<MaterialBuilder, VertexPositionNormal, VertexTexture1, VertexEmpty>>();
            //This value is set to one, if a TextureID of -1 exists
            int IndexModifier = 0;

            if (orderedMeshData.textureID.Contains(-1))
            {
                IndexModifier = 1;
            }
            else
            {
                IndexModifier = 0;
            }
            for (int i = 0; i != orderedMeshData.textureID.Distinct().ToList().Count - IndexModifier; i++)
            {
                gltfPrimitives.Add(sceneMesh.UsePrimitive(gltfMaterials[i], 3));

            }

            for (int i = 0; i < orderedMeshData.verts.Count; i += 3)
            {
                VertexPositionNormal v1 = new VertexPositionNormal(orderedMeshData.verts[i], orderedMeshData.normals[i]);
                VertexPositionNormal v2 = new VertexPositionNormal(orderedMeshData.verts[i + 1], orderedMeshData.normals[i + 1]);
                VertexPositionNormal v3 = new VertexPositionNormal(orderedMeshData.verts[i + 2], orderedMeshData.normals[i + 2]);
                //UV coords
                // XYZ here is meaningless, just accessors 
                VertexTexture1 vt1 = new VertexTexture1(new Vector2(orderedMeshData.U[i / 3].X, orderedMeshData.V[i / 3].X));
                VertexTexture1 vt2 = new VertexTexture1(new Vector2(orderedMeshData.U[i / 3].Y, orderedMeshData.V[i / 3].Y));
                VertexTexture1 vt3 = new VertexTexture1(new Vector2(orderedMeshData.U[i / 3].Z, orderedMeshData.V[i / 3].Z));

                VertexBuilder<VertexPositionNormal, VertexTexture1, VertexEmpty> ver1 = new VertexBuilder<VertexPositionNormal, VertexTexture1, VertexEmpty>(v1, vt1);
                VertexBuilder<VertexPositionNormal, VertexTexture1, VertexEmpty> ver2 = new VertexBuilder<VertexPositionNormal, VertexTexture1, VertexEmpty>(v2, vt2);
                VertexBuilder<VertexPositionNormal, VertexTexture1, VertexEmpty> ver3 = new VertexBuilder<VertexPositionNormal, VertexTexture1, VertexEmpty>(v3, vt3);

                if(orderedMeshData.textureID[i / 3] == -1)
                {

                }
                else
                {
                    gltfPrimitives[orderedMeshData.textureID[i / 3]].AddTriangle(ver1, ver2, ver3);
                }

               // primitives.AddTriangle(ver1, ver2, ver3);
            }

            SharpGLTF.Scenes.SceneBuilder scene = new SharpGLTF.Scenes.SceneBuilder();
            //https://www.energid.com/resources/orientation-calculator
            //that site converts from sane numbers to whatever backward crackhead numerical system that quaternions use
            //no, I don't care about gymbal lock, my brother in christ you have been played for an absolute fool
            Quaternion fixRotation = new Quaternion(0, 0, 1, 0);
            SharpGLTF.Transforms.AffineTransform defaultTransform = new SharpGLTF.Transforms.AffineTransform(Vector3.One, fixRotation, Vector3.Zero);

            scene.AddRigidMesh(sceneMesh, defaultTransform);

            SharpGLTF.Schema2.ModelRoot model = scene.ToGltf2();

            model.SaveGLTF(outputName);

        }
    }
}