using Godot;
using System;
using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Linq;


using ChunkCoord = Godot.Vector2I;
using AtomHolder = System.Collections.Generic.List<AtomInstance>;

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
		if (!ValidateBond(atomA, atomB)) return false;
		atomA.bonds.Add(atomB);
		atomB.bonds.Add(atomA);
		return true;
	}

	public void Simulate(float delta)
	{
		// AtomPhysicsCPU.Simulate(chunks);
		AtomPhysicsGPU.Simulate(chunks, delta);

		UpdateAtomPositions(delta);

		// --- Reassign atoms to new chunks ---
		var chunkKeys = chunks.Keys.ToList();
		foreach (var kvp in chunkKeys)
		{
			var atoms = chunks[kvp].ToList();
			foreach (var atom in atoms)
			{
				var newChunk = GetChunkCoord(atom.position);
				if (newChunk != kvp)
				{
					chunks[kvp].Remove(atom); // Remove from old chunk
					AddAtomToChunk(atom); // Add to new chunk
				}

				for (int i = atom.bonds.Count - 1; i >= 0; i--)
				{
					var bond = atom.bonds[i];
					float dist = (atom.position - bond.position).Length();
					float meanBondLength = GetMeanBondLength(atom, bond);
					if (dist > meanBondLength * breakBondDistance)
					{
						BreakBond(atom, bond);
					}
				}
			}
		}
	}

	// --- PRIVATE HELPERS ---
	private float GetMeanBondLength(AtomInstance atomA, AtomInstance atomB)
	{
		return (atomA.r_e + atomB.r_e) * 0.5f;
	}

	public bool BreakBond(AtomInstance atomA, AtomInstance atomB)
	{
		var a = atomA.bonds.Remove(atomB);
		var b = atomB.bonds.Remove(atomA);
		return a || b;
	}

	private void UpdateAtomPositions(float delta)
	{
		float atomDamping = 0.95f; // You may want to expose this as a parameter
		foreach (var chunkAtoms in chunks.Values)
		{
			foreach (var atom in chunkAtoms)
			{
				atom.position += atom.velocity * delta;
				atom.velocity *= atomDamping;
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
	bool ValidateBond(AtomInstance atomA, AtomInstance atomB)
	{
		if (atomA == null || atomB == null || atomA == atomB)
		{
			GD.Print("null");
			return false; // Check if both atoms are valid and not already bonded
		}
		if (atomA.bonds.Contains(atomB) || atomB.bonds.Contains(atomA))
		{
			GD.Print("not contains");
			return false; // Bond already exists
		}
		if (GetMeanBondLength(atomA, atomB) * breakBondDistance <= Distance(atomA, atomB))
		{
			GD.Print("too far");
			return false; // Bond distance is too large
		}
		return true;
	}
	public ChunkCoord GetChunkCoord(Vector2 position) //AtomInstance atom
	{
		return new ChunkCoord((int)(position.X / chunkSize.X), (int)(position.Y / chunkSize.Y));
	}

	// stuff
	public bool IsBonded(AtomInstance atomA, AtomInstance atomB)
	{
		if (atomA == null || atomB == null) return false;
		return atomA.bonds.Contains(atomB) || atomB.bonds.Contains(atomA);
	}
}
