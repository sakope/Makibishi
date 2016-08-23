using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace MeshUtility
{
    public class MeshConverter
    {
        [MenuItem("Assets/ExtractMesh/TriangleTopology")]
        public static void ExtractTriangleMesh()
        {
            ExtractMesh(MeshTopology.Triangles);
        }

        [MenuItem("Assets/ExtractMesh/DividedTriangleTopology")]
        public static void ExtractDividedTriangleMesh()
        {
            ExtractMesh(MeshTopology.Triangles, false);
        }

        [MenuItem("Assets/ExtractMesh/LineTopology")]
        public static void ExtractLineMesh()
        {
            ExtractMesh(MeshTopology.Lines);
        }

        [MenuItem("Assets/ExtractMesh/DividedLineTopology")]
        public static void ExtractDividedLineMesh()
        {
            ExtractMesh(MeshTopology.Lines, false);
        }

        [MenuItem("Assets/ExtractMesh/LineStripTopology")]
        public static void ExtractLineStripMesh()
        {
            ExtractMesh(MeshTopology.LineStrip);
        }

        [MenuItem("Assets/ExtractMesh/DividedLineStripTopology")]
        public static void ExtractDividedLineStripMesh()
        {
            ExtractMesh(MeshTopology.LineStrip, false);
        }

        [MenuItem("Assets/ExtractMesh/PointTopology")]
        public static void ExtractPointMesh()
        {
            ExtractMesh(MeshTopology.Points);
        }

        [MenuItem("Assets/ExtractMesh/DividedPointTopology")]
        public static void ExtractDividedPointMesh()
        {
            ExtractMesh(MeshTopology.Points, false);
        }

        [MenuItem("Assets/ExtractMesh/QuadTopology")]
        public static void ExtractQuadMesh()
        {
            ExtractMesh(MeshTopology.Quads);
        }

        [MenuItem("Assets/ExtractMesh/DividedQuadTopology")]
        public static void ExtractDividedQuadMesh()
        {
            ExtractMesh(MeshTopology.Quads, false);
        }

        private static void ExtractMesh(MeshTopology meshTopology, bool useIBO = true)
        {
            IEnumerable<MeshFilter> MeshFilters = Selection.gameObjects.Where(o => o.GetComponent<MeshFilter>()).Select((o) => o.GetComponent<MeshFilter>());
            string assetPath = Path.GetDirectoryName(AssetDatabase.GetAssetPath(Selection.objects[0]));
            foreach (MeshFilter meshFilter in MeshFilters)
            {
                Mesh mesh = meshFilter.sharedMesh;
                if (mesh)
                {
                    Mesh convertedMesh = new Mesh();
                    convertedMesh = ConvertMesh(mesh, meshTopology, useIBO);
                    AssetDatabase.CreateAsset(convertedMesh, string.Format("{0}/{1}{2}{3}.asset", assetPath, mesh.name, useIBO ? "" : "WithoutIBO", meshTopology.ToString()));
                    AssetDatabase.SaveAssets();
                }
            }
        }

        private static Mesh ConvertMesh(Mesh mesh, MeshTopology meshTopology, bool useIBO)
        {
            Mesh convertMesh = new Mesh();

            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            Vector2[] uv = mesh.uv;
            int[] indexes = mesh.triangles;

            Color32[] colors32 = (Color32[])Enumerable.Empty<Color32>();
            Vector2[] uv2 = (Vector2[])Enumerable.Empty<Vector2>();
            Vector2[] uv3 = (Vector2[])Enumerable.Empty<Vector2>();
            BoneWeight[] boneWeight = (BoneWeight[])Enumerable.Empty<BoneWeight>();

            if (mesh.colors32.Length > 0)
                colors32 = mesh.colors32;
            if (mesh.uv2.Length > 0)
                uv2 = mesh.uv2;
            if (mesh.uv3.Length > 0)
                uv3 = mesh.uv3;
            if (mesh.boneWeights.Length > 0)
                boneWeight = mesh.boneWeights;

            if (!useIBO)
            {
                Vector3[] divideVertices = new Vector3[indexes.Length];
                Vector3[] divideNormals = new Vector3[indexes.Length];
                Vector2[] divideUV = new Vector2[indexes.Length];
                int[] divideIndexes = new int[indexes.Length];
                Color32[] divideColor32 = (Color32[])Enumerable.Empty<Color32>();
                Vector2[] divideUV2 = (Vector2[])Enumerable.Empty<Vector2>();
                Vector2[] divideUV3 = (Vector2[])Enumerable.Empty<Vector2>();
                BoneWeight[] divideBoneWeight = (BoneWeight[])Enumerable.Empty<BoneWeight>();
                if (colors32.Length > 0)
                    divideColor32 = new Color32[indexes.Length];
                if (uv2.Length > 0)
                    divideUV2 = new Vector2[indexes.Length];
                if (uv3.Length > 0)
                    divideUV3 = new Vector2[indexes.Length];
                if (boneWeight.Length > 0)
                    divideBoneWeight = new BoneWeight[indexes.Length];

                for (int i = 0; i < indexes.Length; i++)
                {
                    int index = indexes[i];
                    divideVertices[i] = vertices[index];
                    divideNormals[i] = normals[index];
                    divideUV[i] = uv[index];
                    divideIndexes[i] = i;

                    if (colors32.Length > 0)
                        divideColor32[i] = colors32[index];
                    if (uv2.Length > 0)
                        divideUV2[i] = uv2[index];
                    if (uv3.Length > 0)
                        divideUV3[i] = uv3[index];
                    if (boneWeight.Length > 0)
                        divideBoneWeight[i] = boneWeight[i];
                }

                convertMesh.vertices = divideVertices;
                convertMesh.normals = divideNormals;
                convertMesh.uv = divideUV;
                convertMesh.SetIndices(divideIndexes, meshTopology, 0);
                if (divideColor32.Length > 0)
                    convertMesh.colors32 = divideColor32;
                if (divideUV2.Length > 0)
                    convertMesh.uv2 = divideUV2;
                if (divideUV3.Length > 0)
                    convertMesh.uv3 = divideUV3;
                if (divideBoneWeight.Length > 0)
                    convertMesh.boneWeights = boneWeight;
            }
            else
            {
                convertMesh.vertices = vertices;
                convertMesh.normals = normals;
                convertMesh.uv = uv;
                convertMesh.SetIndices(indexes, meshTopology, 0);
                if (colors32.Length > 0)
                    convertMesh.colors32 = colors32;
                if (uv2.Length > 0)
                    convertMesh.uv2 = uv2;
                if (uv3.Length > 0)
                    convertMesh.uv3 = uv3;
                if (boneWeight.Length > 0)
                    convertMesh.boneWeights = boneWeight;
            }
            return convertMesh;
        }
    }
}
