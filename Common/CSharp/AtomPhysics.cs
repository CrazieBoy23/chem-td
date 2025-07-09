using Godot;
using System;
using System.Collections.Generic;
using System.Reflection.Metadata;


using ChunkCoord = Godot.Vector2I;
using ChunkHolder = System.Collections.Generic.List<AtomInstance>;

public class AtomInstance
{
    public Vector2 position;
    public Vector2 velocity;

    public int maxConnections;
    public float D_e, a, r_e, extended_modifier, charge, mass, radius;

    public Node2D node; // Reference to the Godot Node2D instance representing this atom
}

public class AtomPhysics
{
    public Dictionary<ChunkCoord, ChunkHolder> chunks;
    Vector2 chunkSize;

    public AtomPhysics(Vector2 chunkSize)
    {
        this.chunkSize = chunkSize;
        chunks = new Dictionary<ChunkCoord, ChunkHolder>();
    }

    public bool AddAtom(AtomInstance atom)
    {
        ChunkCoord chunkCoord = GetChunkCoord(atom); //new ChunkCoord((int)(atom.position.x / chunkSize.x), (int)(atom.position.y / chunkSize.y));
        if (!chunks.ContainsKey(chunkCoord))
        {
            chunks[chunkCoord] = new ChunkHolder();
        }
        chunks[chunkCoord].Add(atom);
        return true;
    }

    ChunkCoord GetChunkCoord(AtomInstance atom)
    {
        return new ChunkCoord((int)(atom.position.X / chunkSize.X), (int)(atom.position.Y / chunkSize.Y));
    }
}
