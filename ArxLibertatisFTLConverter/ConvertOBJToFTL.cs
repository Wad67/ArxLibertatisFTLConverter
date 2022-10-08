using ArxLibertatisEditorIO.MediumIO.FTL;
using ArxLibertatisEditorIO.RawIO.FTL;
using ArxLibertatisEditorIO.Util;
using CSWavefront.Raw;
using CSWavefront.Util;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Numerics;

namespace ArxLibertatisFTLConverter
{
    public static class ConvertOBJToFTL
    {
        private class EqualsComparer<T> : IEqualityComparer<T>
        {
            public bool Equals([AllowNull] T x, [AllowNull] T y)
            {
                if (x == null && y == null)
                {
                    return true;
                }
                else if (x == null || y == null)
                {
                    return false;
                }

                return x.Equals(y);
            }

            public int GetHashCode([DisallowNull] T obj)
            {
                return obj.GetHashCode();
            }
        }

        private static Vertex FromPolyVertex(ObjFile obj, PolygonVertex polyVertex)
        {
            Vertex v = new Vertex();
            Vector4 pos = obj.vertices[polyVertex.vertex];
            Vector3 norm = obj.normals[polyVertex.normal];
            v.vertex = new Vector3(pos.X, -pos.Y, pos.Z);
            v.normal = new Vector3(norm.X, -norm.Y, norm.Z);
            return v;
        }

        public static void Convert(string file)
        {
            ObjFile obj = ObjLoader.Load(file);
            string mtlFile = Path.Join(Path.GetDirectoryName(file), Path.GetFileNameWithoutExtension(file) + ".mtl");
            string ftlFile = Path.Join(Path.GetDirectoryName(file), Path.GetFileNameWithoutExtension(file) + ".ftl");
            MtlFile mtl = new MtlFile();
            if (File.Exists(mtlFile))
            {
                mtl = MtlLoader.Load(mtlFile);
            }

            Ftl ftl = new Ftl
            {
                dataSection3D = new DataSection3D()
            };
            ftl.dataSection3D.header.name = Path.GetFileNameWithoutExtension(file);

            //materials
            List<Material> materials = new List<Material>();
            AutoDictionary<string, int> materialNameToIndex = new AutoDictionary<string, int>((x) => { Material mat = new Material(x) { diffuseMap = x }; materials.Add(mat); return materials.Count - 1; });
            foreach (KeyValuePair<string, Material> kv in mtl.materials)
            {
                materials.Add(kv.Value);
                materialNameToIndex[kv.Key] = materialNameToIndex.Count;
            }

            //create set of all vertices
            HashSet<Vertex> allVertices = new HashSet<Vertex>(new EqualsComparer<Vertex>());
            foreach (KeyValuePair<string, ObjObject> kv in obj.objects)
            {
                string name = kv.Key;
                ObjObject o = kv.Value;

                foreach (KeyValuePair<string, List<Polygon>> kv2 in o.polygons)
                {
                    List<Polygon> polygons = kv2.Value;
                    for (int i = 0; i < polygons.Count; ++i)
                    {
                        Polygon polygon = polygons[i];
                        for (int j = 0; j < 3; ++j) //only do triangles
                        {
                            PolygonVertex objVert = polygon.vertices[j];

                            Vertex v = FromPolyVertex(obj, objVert);

                            allVertices.Add(v);
                        }
                    }
                }
            }
            //create list of vertices and index lookup
            Dictionary<Vertex, int> vertexToIndex = new Dictionary<Vertex, int>(new EqualsComparer<Vertex>());
            foreach (Vertex v in allVertices)
            {
                vertexToIndex[v] = ftl.dataSection3D.vertexList.Count;
                ftl.dataSection3D.vertexList.Add(v);
            }

            //create face list
            foreach (KeyValuePair<string, ObjObject> kv in obj.objects)
            {
                string name = kv.Key;
                ObjObject o = kv.Value;

                foreach (KeyValuePair<string, List<Polygon>> kv2 in o.polygons)
                {
                    string matName = kv2.Key;
                    int matIndex = materialNameToIndex[matName];
                    List<Polygon> polygons = kv2.Value;

                    for (int i = 0; i < polygons.Count; ++i)
                    {
                        Polygon polygon = polygons[i];
                        Face face = new Face
                        {
                            textureContainerIndex = (short)matIndex
                        };

                        Vector3 faceNormal = Vector3.Zero;

                        for (int j = 0; j < 3; ++j) //only do triangles
                        {
                            PolygonVertex objVert = polygon.vertices[j];
                            Vertex v = FromPolyVertex(obj, objVert);

                            faceNormal += v.normal;

                            int vertexIndex = vertexToIndex[v];

                            Face.EerieFaceVertex ftlVert = face.vertices[j];
                            ftlVert.color = new Color(1, 1, 1);
                            ftlVert.normal = obj.normals[objVert.normal];
                            ftlVert.u = obj.uvs[objVert.uv].X;
                            ftlVert.v = 1 - obj.uvs[objVert.uv].Y;
                            ftlVert.ou = (short)(255 * ftlVert.u);
                            ftlVert.ov = (short)(255 * ftlVert.v);
                            ftlVert.vertexIndex = (ushort)vertexIndex;
                        }
                        face.normal = faceNormal / 3;
                        ftl.dataSection3D.faceList.Add(face);
                    }
                }
            }

            //write materials here cause it couldve been changed by above code
            for (int i = 0; i < materials.Count; ++i)
            {
                ftl.dataSection3D.textures.Add(materials[i].diffuseMap);
            }

            FTL_IO rawFtl = new FTL_IO();
            ftl.WriteTo(rawFtl);
            using (MemoryStream ms = new MemoryStream())
            {
                rawFtl.WriteTo(ms);
                Stream packed = FTL_IO.EnsurePacked(ms);
                using (FileStream fs = new FileStream(ftlFile, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
                {
                    packed.CopyTo(fs);
                }
                packed.Dispose();
            }
        }
    }
}
