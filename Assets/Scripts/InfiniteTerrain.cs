using System.Collections.Generic;
using UnityEngine;

public class InfiniteTerrain : MonoBehaviour
{
    const float scale = 5f;
const float viewerMoveThresholdForChunkUpdate = 25f;
const float sqrtViewerMoveThresholdForChunkUpdate = viewerMoveThresholdForChunkUpdate * viewerMoveThresholdForChunkUpdate; 
   public static float maxViewDst;

   public LODInfo[] detailedLevels;
   public Transform viewer;

   public Material mapMaterial;

   public static Vector2 viewerPosition;
   public Vector2 viewerPositionOld;

   static MapGenerator mapGenerator;

   int chunkSize;
   int chunksVisibleInViewDst;

   Dictionary<Vector2,TerrainChunk> terrainChunkDictionnay = new Dictionary<Vector2, TerrainChunk>();
    static List<TerrainChunk> terrainChunksVisibleLastUpdate = new List<TerrainChunk>();

   private void Start() {
       mapGenerator = FindObjectOfType<MapGenerator>();
       maxViewDst = detailedLevels[detailedLevels.Length-1].visibleDstThreshold;
       chunkSize = MapGenerator.mapChunkSize -1;
       chunksVisibleInViewDst = Mathf.RoundToInt(maxViewDst / chunkSize);

       UpdateVisibleChunks();
   }

   private void Update() {
       viewerPosition = new Vector2(viewer.position.x,viewer.position.z)/scale;

       if((viewerPositionOld - viewerPosition).sqrMagnitude > sqrtViewerMoveThresholdForChunkUpdate){
           viewerPositionOld = viewerPosition;
           UpdateVisibleChunks();
       }
   }

   void UpdateVisibleChunks(){

       for (var i = 0; i < terrainChunksVisibleLastUpdate.Count ; i++)
       {
           terrainChunksVisibleLastUpdate[i].SetVisible(false);
       }
       terrainChunksVisibleLastUpdate.Clear();

       int currentChunkCoordX = Mathf.RoundToInt(viewerPosition.x / chunkSize);
       int currentChunkCoordY = Mathf.RoundToInt(viewerPosition.y / chunkSize);

       for (var yOffset = -chunksVisibleInViewDst; yOffset <= chunksVisibleInViewDst; yOffset++)
       {
            for (var xOffset = -chunksVisibleInViewDst; xOffset <= chunksVisibleInViewDst; xOffset++)
                {
                    Vector2 viewedChunkCoord = new Vector2(currentChunkCoordX + xOffset,currentChunkCoordY + yOffset);

                    if(terrainChunkDictionnay.ContainsKey(viewedChunkCoord)){
                        terrainChunkDictionnay[viewedChunkCoord].UpdateTerrainChunk();
                        
                    }
                    else{
                        terrainChunkDictionnay.Add(viewedChunkCoord,new TerrainChunk(viewedChunkCoord,chunkSize,detailedLevels,transform,mapMaterial));
                    }

                }
       }

   }
    public class TerrainChunk{

        Vector2 position;

        GameObject meshObject;

        Bounds bounds;
        MeshRenderer meshRenderer;

        LODInfo[] detailLevels;

        LODMesh[] lODMeshes;

        MeshFilter meshFilter;

        MapData mapData;
        bool mapDataReceived;

        int previousLODIndex = -1;

        public TerrainChunk(Vector2 coord, int size,LODInfo[] detailLevels, Transform parent, Material material){
            position = coord * size;
            this.detailLevels = detailLevels;
            bounds = new Bounds(position,Vector2.one * size);
            Vector3 positionV3 = new Vector3(position.x,0,position.y);

            meshObject = new GameObject("TerrainChunk");
            meshRenderer = meshObject.AddComponent<MeshRenderer>();
            meshFilter = meshObject.AddComponent<MeshFilter>();
            meshRenderer.material = material;
            
            meshObject.transform.position = positionV3 * scale;
            meshObject.transform.parent = parent;
            meshObject.transform.localScale = Vector3.one * scale;
            SetVisible(false);

            lODMeshes = new LODMesh[detailLevels.Length];
            for (var i = 0; i < detailLevels.Length; i++)
            {
                lODMeshes[i] = new LODMesh(detailLevels[i].lod, UpdateTerrainChunk);
            }
            mapGenerator.RequestMapData(position,OnMapDataReceived);

        }

        void OnMapDataReceived(MapData mapData){
           this.mapData = mapData;
           mapDataReceived = true;

           Texture2D texture = TextureGenerator.TextureFromColorMap(mapData.colorMap,  MapGenerator.mapChunkSize,MapGenerator.mapChunkSize);
           meshRenderer.material.mainTexture = texture;
           UpdateTerrainChunk();
        }

        // void OnMeshDataReceived(MeshData meshData){
        //     meshFilter.mesh = meshData.CreateMesh();
        // }

        public void UpdateTerrainChunk() {

            if(mapDataReceived){

            
            float viewerDistanceFromNearestEdge = Mathf.Sqrt(bounds.SqrDistance(viewerPosition));
            bool visible = viewerDistanceFromNearestEdge <= maxViewDst;

            if(visible){
                int lodIndex = 0;
                for (var i = 0; i < detailLevels.Length -1; i++)
                {
                 if(viewerDistanceFromNearestEdge > detailLevels[i].visibleDstThreshold){
                     lodIndex = i+1;
                 }
                 else{
                     break;
                 }
                }
                if(lodIndex != previousLODIndex){
                    LODMesh lodMesh = lODMeshes[lodIndex];
                    if(lodMesh.hasMesh){
                        previousLODIndex = lodIndex;
                        meshFilter.mesh = lodMesh.mesh;
                    }
                    else if(!lodMesh.hasRequestedMesh){
                        lodMesh.RequestMesh(mapData);
                    }
                }
            }
            terrainChunksVisibleLastUpdate.Add(this);
            SetVisible(visible);
            }
        }

        public void SetVisible(bool visible){
            meshObject.SetActive(visible);
        }

        public bool IsVisible(){
            return meshObject.activeSelf;
        }
    }

    class LODMesh{
        public Mesh mesh;
        public bool hasRequestedMesh;

        public bool hasMesh;
        public int lod;
        System.Action updateCallback;

        public LODMesh(int lod, System.Action updateCallback){
            this.lod = lod;
            this.updateCallback = updateCallback;
        }

        void OnMeshDataReceived(MeshData meshData){
            mesh = meshData.CreateMesh();
            hasMesh = true;
            updateCallback();
        }

        public void RequestMesh(MapData mapData){
            hasRequestedMesh = true;
            mapGenerator.RequestMeshData(mapData, lod ,OnMeshDataReceived);
        }

    }
    [System.Serializable]
    public struct LODInfo{
        public int lod;
        public float visibleDstThreshold;
    }
}


