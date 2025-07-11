using Godot;
using System;
using System.Collections.Generic;
using System.Reflection.Metadata;


using ChunkCoord = Godot.Vector2I;
using AtomHolder = System.Collections.Generic.List<AtomInstance>;
using System.Linq;

public class AtomInstance
{
	public Vector2 position;
	public Vector2 velocity;

	public int maxConnections;
	public float D_e, a, r_e, extended_modifier, charge, mass, radius;


	public AtomHolder bonds = new AtomHolder(); // List of bonded atoms
	public Node2D node; // Reference to the Godot Node2D instance representing this atom
}

public class AtomPhysics
{
	public Dictionary<ChunkCoord, AtomHolder> chunks;

	Vector2 chunkSize;
	float breakBondDistance;

	public AtomPhysics(Vector2 chunkSize, float breakBondDistance)
	{
		this.chunkSize = chunkSize;
		this.breakBondDistance = breakBondDistance;
		chunks = new Dictionary<ChunkCoord, AtomHolder>();
	}

	public bool AddAtomToChunk(AtomInstance atom)
	{
		ChunkCoord chunkCoord = GetChunkCoord(atom.position); //new ChunkCoord((int)(atom.position.x / chunkSize.x), (int)(atom.position.y / chunkSize.y));
		if (!chunks.ContainsKey(chunkCoord))
		{
			chunks[chunkCoord] = new AtomHolder();
		}
		chunks[chunkCoord].Add(atom);
		return true;
	}

	public bool RemoveAtom(AtomInstance atom)
	{
		ChunkCoord chunkCoord = GetChunkCoord(atom.position);
		if (chunks.ContainsKey(chunkCoord))
		{
			var atomList = chunks[chunkCoord];
			if (atomList.Remove(atom))
			{
				// If the chunk is empty after removal, remove the chunk itself
				atom.node?.QueueFree(); // Free the node if it exists
				if (atomList.Count == 0)
				{
					chunks.Remove(chunkCoord);
				}
				return true; // Successfully removed atom
			}
		}
		return false; // Atom not found in chunk it should be in (it might be in another chunk idk lol it shouldnt)
	}

	public bool AddAtomToChunk(AtomInstance atom, AtomInstance bondTo)
	{
		// Add the atom to the chunk
		if (!AddAtomToChunk(atom))
		{
			return false; // Failed to add atom to chunk
		}
		if (!AddBond(atom, bondTo))
		{
			return false; // Failed to add bond
		}
		return true;
	}

	public bool AddBond(AtomInstance atomA, AtomInstance atomB)
	{
		if (!validateBond(atomA, atomB)) return false;
		atomA.bonds.Add(atomB);
		atomB.bonds.Add(atomA);
		return true;
	}

	public void Simulate(float delta)
	{
		/*
		here is the old gdscript code for reference
		for atom_a in bonds.keys():
		for atom_b in bonds[atom_a]:
			var pair_key = _get_pair_key(atom_a, atom_b)
			if processed_pairs.has(pair_key):
				continue
			processed_pairs[pair_key] = true

			var dist = (atom_a.position - atom_b.position).length()
			if dist > _get_mean_bond_length(atom_a, atom_b) * break_bond_distance:
				break_bond(atom_a, atom_b)
			else:
				_apply_bond_force(atom_a, atom_b)
		*/


		// update positions of atoms in each chunk
		foreach (var kvp in chunks)
		{
			foreach (var atom in kvp.Value)
			{
				ChunkCoord oldChunk = kvp.Key;
				atom.position += atom.velocity * delta;
				ChunkCoord newChunk = GetChunkCoord(atom.position);
				if (oldChunk != newChunk)
				{
					// Atom has moved to a new chunk, remove it from the old chunk
					kvp.Value.Remove(atom);
					if (kvp.Value.Count == 0)
					{
						chunks.Remove(kvp.Key);
					}

					// Add it to the new chunk
					AddAtomToChunk(atom);
					GD.Print($"Atom moved from {oldChunk} to {newChunk}");
				}
			}
		}
	}

	// helpers
	public AtomInstance GetAtomByNode(Node2D node)
	{
		foreach (var atm in chunks[GetChunkCoord(node.Position)])
		{
			if (atm.node == node)
			{
				return atm;
			}
		}
		return null;
	}
	float Distance(AtomInstance atomA, AtomInstance atomB)
	{
		return (atomA.position - atomB.position).Length();
	}
	bool validateBond(AtomInstance atomA, AtomInstance atomB)
	{
		if (atomA == null || atomB == null || atomA == atomB)
		{
			return false; // Check if both atoms are valid and not already bonded
		}
		if (atomA.bonds.Contains(atomB) || atomB.bonds.Contains(atomA))
		{
			return false; // Bond already exists
		}
		if (Mathf.Lerp(atomA.r_e, atomB.r_e, 0.5f) * breakBondDistance <= Distance(atomA, atomB))
		{
			return false; // Bond distance is too large
		}
		return true;
	}
	public ChunkCoord GetChunkCoord(Vector2 position) //AtomInstance atom
	{
		return new ChunkCoord((int)(position.X / chunkSize.X), (int)(position.Y / chunkSize.Y));
	}
}
