using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Threading;

public class MapGenerator : MonoBehaviour
{
    public enum DrawMode{NoiseMap, ColorMap, Mesh, FallOffMap}
    public DrawMode drawMode;

    public Noise.NormalizedMode normalizedMode;

    public const int mapChunkSize = 239;
    [Range(0,6)]
    public int editorPreviewLOD;

    public int seed;
    public float noiseScale;

    public int octaves;

    [Range(0,1)]
    public float persistance;

    public float lacunarity;

    public Vector2 offset;

    public float meshHeightMultiplier;

    public AnimationCurve meshHeightCurve;
    public bool autoUpdate;

    public bool useFallOff;

    public TerrainType[] regions;

    float[,] fallOffMap;

    Queue<MapThreadInfo<MapData>> mapDataThreadInfoQueue = new Queue<MapThreadInfo<MapData>>();
    Queue<MapThreadInfo<MeshData>> meshDataThreadInfoQueue = new Queue<MapThreadInfo<MeshData>>();

private void Awake() {
    fallOffMap = FallOffGenerator.GenerateFallOffofMap(mapChunkSize);
}

    public void DrawMapInEditor(){

        MapData mapData = GenerateMapData(Vector2.zero);
          MapDisplay display = FindObjectOfType<MapDisplay>();
        if(drawMode == DrawMode.NoiseMap){
            display.DrawTexture(TextureGenerator.TextureFromHeightMap(mapData.heightMap));
        }
        else if(drawMode == DrawMode.ColorMap){
            display.DrawTexture(TextureGenerator.TextureFromColorMap(mapData.colorMap,mapChunkSize,mapChunkSize));

        }
        else if(drawMode == DrawMode.Mesh){
            display.DrawMesh(MeshGenerator.GenerateTerrainMesh(mapData.heightMap,meshHeightMultiplier, meshHeightCurve,editorPreviewLOD
    ), TextureGenerator.TextureFromColorMap(mapData.colorMap,mapChunkSize,mapChunkSize));
        }
        else if(drawMode == DrawMode.FallOffMap){
            display.DrawTexture(TextureGenerator.TextureFromHeightMap(FallOffGenerator.GenerateFallOffofMap(mapChunkSize)));
        }

    }

    public void RequestMapData(Vector2 center,Action<MapData> callback){
        ThreadStart threadStart = delegate{
            MapDataThread(center,callback);
        };

        new Thread(threadStart).Start();
    }

    public void MapDataThread(Vector2 center,Action<MapData> callback){
        MapData mapData = GenerateMapData(center);
        lock(mapDataThreadInfoQueue)
        {
            mapDataThreadInfoQueue.Enqueue(new MapThreadInfo<MapData>(callback,mapData));
        }
    }

    public void RequestMeshData(MapData mapData,int lod, Action<MeshData> callback){
        ThreadStart threadStart = delegate{
            MeshDataThread(mapData,lod,callback);
        };

        new Thread(threadStart).Start();

    }

    public void MeshDataThread(MapData mapData,int lod, Action<MeshData> callback){
        MeshData meshData = MeshGenerator.GenerateTerrainMesh(mapData.heightMap,meshHeightMultiplier,meshHeightCurve,lod);
        lock(meshDataThreadInfoQueue)
        {
            meshDataThreadInfoQueue.Enqueue(new MapThreadInfo<MeshData>(callback,meshData));
        }
    }



    private void Update() {
        if(mapDataThreadInfoQueue.Count > 0){
            for (var i = 0; i < mapDataThreadInfoQueue.Count; i++)
            {
                MapThreadInfo<MapData> threadInfo = mapDataThreadInfoQueue.Dequeue();
                threadInfo.callback(threadInfo.parameter);
            }
        }
        if(meshDataThreadInfoQueue.Count > 0){
            for (var i = 0; i < meshDataThreadInfoQueue.Count; i++)
            {
                MapThreadInfo<MeshData> threadInfo = meshDataThreadInfoQueue.Dequeue();
                threadInfo.callback(threadInfo.parameter);
            }
        }
    }

    MapData GenerateMapData(Vector2 center){
        float[,] noiseMap = Noise.GenerateNoiseMap(mapChunkSize+2,mapChunkSize+2,seed,noiseScale,octaves,persistance,lacunarity,center + offset, normalizedMode);

        Color[] colorMap = new Color[mapChunkSize*mapChunkSize];
        
        for (var y = 0; y < mapChunkSize; y++)
        {
            for (var x = 0; x < mapChunkSize; x++)
            {
                if(useFallOff){
                    noiseMap[x,y] = Mathf.Clamp01( noiseMap[x,y] - fallOffMap[x,y]);
                }
                float currentHeight = noiseMap[x,y];
                for (var i = 0; i < regions.Length; i++)
                {
                    if(currentHeight >= regions[i].height){
                        
                        colorMap[y * mapChunkSize + x] = regions[i].color; 
                        
                        
                    }
                    else{
                        break;
                    }
                }
            }
            
        }

        return new MapData(noiseMap,colorMap);

      
    }

    private void OnValidate() {



        if(lacunarity<1){
            lacunarity = 1;
        }
        if(octaves <0){
            octaves = 0;
        }
        fallOffMap = FallOffGenerator.GenerateFallOffofMap(mapChunkSize);


    }

    struct MapThreadInfo<T>{
        public readonly Action<T> callback;
        public readonly T parameter;

        public MapThreadInfo(Action<T> callback, T parameter){
            this.callback = callback;
            this.parameter = parameter;
        }
    }

}
[System.Serializable]
public struct TerrainType{
    public string name;
    public float height;

    public Color color;
}



public struct MapData{
    public readonly float[,] heightMap;
    public readonly Color[] colorMap;

    public MapData(float[,] heightMap, Color[] colorMap){
        this.heightMap = heightMap;
        this.colorMap = colorMap;
    }
}
