using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public static class AtomPhysicsGPU
{
    private const string ComputeShaderPath = "res://Common/Shaders/compute_atom_physics.glsl";

    public struct ChunkInfo
    {
        public int startIndex;
        public int count;
    }

    // Cached GPU resources
    private static RenderingDevice rd;
    private static Godot.Rid shader;
    private static Godot.Rid pipeline;
    private static bool initialized = false;

    private static void EnsureInitialized()
    {
        if (initialized) return;
        rd = RenderingServer.CreateLocalRenderingDevice();
        var shaderFile = GD.Load<RDShaderFile>(ComputeShaderPath);
        var shaderSpirV = shaderFile.GetSpirV();
        shader = rd.ShaderCreateFromSpirV(shaderSpirV);
        pipeline = rd.ComputePipelineCreate(shader);
        initialized = true;
    }

    public static void Simulate(Dictionary<Vector2I, List<AtomInstance>> chunks, float delta)
    {
        EnsureInitialized();

        // Flatten atoms and build chunk info
        var allAtoms = new List<AtomInstance>();
        var chunkInfos = new List<ChunkInfo>();


        // square array for 2d packing
        Vector2I minc = chunks.Keys.First();
        Vector2I maxc = chunks.Keys.First();
        foreach (var pos in chunks.Keys)
        {
            if (pos.X < minc.X) minc.X = pos.X;
            if (pos.Y < minc.Y) minc.Y = pos.Y;
            if (pos.X > maxc.X) maxc.X = pos.X;
            if (pos.Y > maxc.Y) maxc.Y = pos.Y;
        }

        for (int y = minc.Y; y <= maxc.Y; y++)
        {
            for (int x = minc.X; x <= maxc.X; x++)
            {
                var chunkKey = new Vector2I(x, y);
                if (chunks.TryGetValue(chunkKey, out var atomList))
                {
                    int start = allAtoms.Count;
                    allAtoms.AddRange(atomList);
                    chunkInfos.Add(new ChunkInfo { startIndex = start, count = atomList.Count });
                }
            }
        }
        int rowCount = maxc.Y - minc.Y + 1;
        int colCount = maxc.X - minc.X + 1;

        // foreach (var kvp in chunks)
        // {
        //     int start = allAtoms.Count;
        //     allAtoms.AddRange(kvp.Value);
        //     chunkInfos.Add(new ChunkInfo { startIndex = start, count = kvp.Value.Count });
        // }

        // Flatten bonds index and count
        var bondIndices = new List<int>();
        var bondOffset = new List<int>();
        var bondCount = new List<int>();
        // foreach (var atom in allAtoms)
        // {
        //     bondOffset.Add(bondIndices.Count);
        //     bondCount.Add(atom.bonds.Count);
        //     foreach (var bond in atom.bonds)
        //         bondIndices.Add(allAtoms.IndexOf(bond));
        // }
        var atomToIndex = new Dictionary<AtomInstance, int>();
        for (int i = 0; i < allAtoms.Count; i++)
            atomToIndex[allAtoms[i]] = i;

        foreach (var atom in allAtoms)
        {
            bondOffset.Add(bondIndices.Count);
            bondCount.Add(atom.bonds.Count);
            foreach (var bond in atom.bonds)
                bondIndices.Add(atomToIndex[bond]);
        }

        // define storage buffers for atom data, chunk info, and bonds
        int[] bondIndicesData = bondIndices.ToArray();
        int[] bondOffsetData = bondOffset.ToArray();
        int[] bondCountData = bondCount.ToArray();

        // Convert lists to byte arrays
        byte[] bondIndicesBytes = new byte[bondIndices.Count * sizeof(int)];
        byte[] bondOffsetBytes = new byte[bondOffset.Count * sizeof(int)];
        byte[] bondCountBytes = new byte[bondCount.Count * sizeof(int)];

        // Copy bond data to byte arrays
        Buffer.BlockCopy(bondIndicesData, 0, bondIndicesBytes, 0, bondIndicesBytes.Length);
        Buffer.BlockCopy(bondOffsetData, 0, bondOffsetBytes, 0, bondOffsetBytes.Length);
        Buffer.BlockCopy(bondCountData, 0, bondCountBytes, 0, bondCountBytes.Length);

        // Atom buffer: [pos.x, pos.y, vel.x, vel.y, mass, charge, radius, ...]
        float[] atomData = new float[allAtoms.Count * 8];
        float[] additionalData = new float[allAtoms.Count * 4];
        for (int i = 0; i < allAtoms.Count; i++)
        {
            var atom = allAtoms[i];
            atomData[i * 8 + 0] = atom.position.X;
            atomData[i * 8 + 1] = atom.position.Y;
            atomData[i * 8 + 2] = atom.velocity.X;
            atomData[i * 8 + 3] = atom.velocity.Y;
            atomData[i * 8 + 4] = atom.mass;
            atomData[i * 8 + 5] = atom.charge;
            atomData[i * 8 + 6] = atom.radius;
            atomData[i * 8 + 7] = 0;

            additionalData[i * 4 + 0] = atom.D_e;
            additionalData[i * 4 + 1] = atom.a;
            additionalData[i * 4 + 2] = atom.r_e;
            additionalData[i * 4 + 3] = atom.extended_modifier;
        }
        byte[] atomBytes = new byte[atomData.Length * sizeof(float)];
        byte[] additionalBytes = new byte[additionalData.Length * sizeof(float)];
        Buffer.BlockCopy(atomData, 0, atomBytes, 0, atomBytes.Length);
        Buffer.BlockCopy(additionalData, 0, additionalBytes, 0, additionalBytes.Length);

        // Chunk info buffer: [startIndex, count] for each chunk
        int[] chunkInfoData = new int[chunkInfos.Count * 2 + 2];
        for (int i = 1; i <= chunkInfos.Count; i++)
        {
            chunkInfoData[i * 2 + 1] = chunkInfos[i - 1].startIndex;
            chunkInfoData[i * 2] = chunkInfos[i - 1].count;
        }
        chunkInfoData[0] = chunkInfos.Count; // Store the number of chunks at the start
        chunkInfoData[1] = rowCount; // Store the row count at the start
        
        byte[] chunkInfoBytes = new byte[chunkInfoData.Length * sizeof(int)];
        Buffer.BlockCopy(chunkInfoData, 0, chunkInfoBytes, 0, chunkInfoBytes.Length);

        // Create storage buffers (these are still per-frame, but much cheaper than pipeline/shader)
        var atomBuffer = rd.StorageBufferCreate((uint)atomBytes.Length, atomBytes);
        var additionalBuffer = rd.StorageBufferCreate((uint)additionalBytes.Length, additionalBytes);
        var chunkInfoBuffer = rd.StorageBufferCreate((uint)chunkInfoBytes.Length, chunkInfoBytes);
        var bondIndicesBuffer = rd.StorageBufferCreate((uint)bondIndicesBytes.Length, bondIndicesBytes);
        var bondOffsetBuffer = rd.StorageBufferCreate((uint)bondOffsetBytes.Length, bondOffsetBytes);
        var bondCountBuffer = rd.StorageBufferCreate((uint)bondCountBytes.Length, bondCountBytes);

        // Uniforms
        var atomUniform = new RDUniform
        {
            UniformType = RenderingDevice.UniformType.StorageBuffer,
            Binding = 0
        };
        atomUniform.AddId(atomBuffer);

        var additionalUniform = new RDUniform
        {
            UniformType = RenderingDevice.UniformType.StorageBuffer,
            Binding = 1
        };
        additionalUniform.AddId(additionalBuffer);

        var chunkUniform = new RDUniform
        {
            UniformType = RenderingDevice.UniformType.StorageBuffer,
            Binding = 2
        };
        chunkUniform.AddId(chunkInfoBuffer);

        var bondIndicesUniform = new RDUniform
        {
            UniformType = RenderingDevice.UniformType.StorageBuffer,
            Binding = 3
        };
        bondIndicesUniform.AddId(bondIndicesBuffer);

        var bondOffsetUniform = new RDUniform
        {
            UniformType = RenderingDevice.UniformType.StorageBuffer,
            Binding = 4
        };
        bondOffsetUniform.AddId(bondOffsetBuffer);

        var bondCountUniform = new RDUniform
        {
            UniformType = RenderingDevice.UniformType.StorageBuffer,
            Binding = 5
        };
        bondCountUniform.AddId(bondCountBuffer);

        var uniforms = new Godot.Collections.Array<Godot.RDUniform> { atomUniform, additionalUniform, chunkUniform, bondIndicesUniform, bondOffsetUniform, bondCountUniform };
        var uniformSet = rd.UniformSetCreate(uniforms, shader, 0);

        // Compute pipeline
        var computeList = rd.ComputeListBegin();
        rd.ComputeListBindComputePipeline(computeList, pipeline);
        rd.ComputeListBindUniformSet(computeList, uniformSet, 0);

        // Dispatch: one invocation per atom (or per chunk, depending on shader logic)
        int workgroupSize = 64;
        int numGroups = (allAtoms.Count + workgroupSize - 1) / workgroupSize;
        rd.ComputeListDispatch(computeList, (uint)numGroups, 1, 1);
        rd.ComputeListEnd();

        rd.Submit();
        rd.Sync();

        // Read back the data from the atom buffer
        var outputBytes = rd.BufferGetData(atomBuffer);
        float[] output = new float[atomData.Length];
        Buffer.BlockCopy(outputBytes, 0, output, 0, outputBytes.Length);

        // Update atom instances
        for (int i = 0; i < allAtoms.Count; i++)
        {
            allAtoms[i].position = new Vector2(output[i * 8 + 0], output[i * 8 + 1]);
            allAtoms[i].velocity = new Vector2(output[i * 8 + 2], output[i * 8 + 3]);
        }

        // Free only per-frame resources
        rd.FreeRid(atomBuffer);
        rd.FreeRid(additionalBuffer);
        rd.FreeRid(chunkInfoBuffer);
        rd.FreeRid(bondIndicesBuffer);
        rd.FreeRid(bondOffsetBuffer);
        rd.FreeRid(bondCountBuffer);
        // Do NOT free pipeline, shader, or device here! GOT ME?!
        return;
    }
}