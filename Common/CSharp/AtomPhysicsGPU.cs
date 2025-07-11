using Godot;
using System;
using System.Collections.Generic;

public static class AtomPhysicsGPU
{
    private const string ComputeShaderPath = "res://Common/Shaders/compute_atom_physics.glsl";

    public struct ChunkInfo
    {
        public int startIndex;
        public int count;
    }

    public static void Simulate(Dictionary<Vector2I, List<AtomInstance>> chunks)
    {
        var rd = RenderingServer.CreateLocalRenderingDevice();
        var shaderFile = GD.Load<RDShaderFile>(ComputeShaderPath);
        var shaderSpirV = shaderFile.GetSpirV();
        var shader = rd.ShaderCreateFromSpirV(shaderSpirV);

        // Flatten atoms and build chunk info
        var allAtoms = new List<AtomInstance>();
        var chunkInfos = new List<ChunkInfo>();
        foreach (var kvp in chunks)
        {
            int start = allAtoms.Count;
            allAtoms.AddRange(kvp.Value);
            chunkInfos.Add(new ChunkInfo { startIndex = start, count = kvp.Value.Count });
        }

        // Atom buffer: [pos.x, pos.y, vel.x, vel.y, mass, charge, radius, ...]
        float[] atomData = new float[allAtoms.Count * 8];
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
        }
        byte[] atomBytes = new byte[atomData.Length * sizeof(float)];
        Buffer.BlockCopy(atomData, 0, atomBytes, 0, atomBytes.Length);

        // Chunk info buffer: [startIndex, count] for each chunk
        int[] chunkInfoData = new int[chunkInfos.Count * 2];
        for (int i = 1; i <= chunkInfos.Count; i++)
        {
            chunkInfoData[i * 2 + 0] = chunkInfos[i].startIndex;
            chunkInfoData[i * 2 + 1] = chunkInfos[i].count;
        }
        chunkInfoData[0] = chunkInfos.Count; // Store the number of chunks at the start
        byte[] chunkInfoBytes = new byte[chunkInfoData.Length * sizeof(int)];
        Buffer.BlockCopy(chunkInfoData, 0, chunkInfoBytes, 0, chunkInfoBytes.Length);

        // Create storage buffers
        var atomBuffer = rd.StorageBufferCreate((uint)atomBytes.Length, atomBytes);
        var chunkInfoBuffer = rd.StorageBufferCreate((uint)chunkInfoBytes.Length, chunkInfoBytes);

        // Uniforms
        var atomUniform = new RDUniform
        {
            UniformType = RenderingDevice.UniformType.StorageBuffer,
            Binding = 0
        };
        atomUniform.AddId(atomBuffer);

        var chunkUniform = new RDUniform
        {
            UniformType = RenderingDevice.UniformType.StorageBuffer,
            Binding = 1
        };
        chunkUniform.AddId(chunkInfoBuffer);

        var uniforms = new Godot.Collections.Array<Godot.RDUniform> { atomUniform, chunkUniform };
        var uniformSet = rd.UniformSetCreate(uniforms, shader, 0);

        // Compute pipeline
        var pipeline = rd.ComputePipelineCreate(shader);
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

        // Free resources
        rd.FreeRid(atomBuffer);
        rd.FreeRid(chunkInfoBuffer);
        rd.FreeRid(pipeline);
        rd.FreeRid(uniformSet);
        rd.FreeRid(shader);
        rd.Free();
    }
}