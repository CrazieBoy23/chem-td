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
		if (!ValidateBond(atomA, atomB)) return false;
		atomA.bonds.Add(atomB);
		atomB.bonds.Add(atomA);
		return true;
	}

	public void Simulate(float delta)
	{
		// --- Bond Processing ---
		foreach (var kvp in chunks)
		{
			foreach (var atomA in kvp.Value.ToList())
			{
				foreach (var atomB in atomA.bonds.ToList())
				{
					if (atomA.GetHashCode() > atomB.GetHashCode()) continue; // Ensure each pair is processed only once

					float dist = (atomA.position - atomB.position).Length();
					float meanBondLength = GetMeanBondLength(atomA, atomB);
					if (dist > meanBondLength * breakBondDistance)
					{
						BreakBond(atomA, atomB);
					}
					else
					{
						ApplyBondForce(atomA, atomB);
					}
				}
			}
		}

		// --- Repel force (using 3x3 chunks) ---
		var allChunkKeys = chunks.Keys.ToList();
		//processedPairs.Clear();
		foreach (var chunkKey in allChunkKeys)
		{
			if (!chunks.ContainsKey(chunkKey)) continue;
			var chunkAtoms = chunks[chunkKey].ToList();
			for (int i = 0; i < chunkAtoms.Count; i++)
			{
				var atomA = chunkAtoms[i];
				for (int offsetX = -1; offsetX <= 1; offsetX++)
				{
					for (int offsetY = -1; offsetY <= 1; offsetY++)
					{
						var neighborKey = new ChunkCoord(chunkKey.X + offsetX, chunkKey.Y + offsetY);
						if (!chunks.ContainsKey(neighborKey)) continue;
						var neighborAtoms = chunks[neighborKey];
						foreach (var atomB in neighborAtoms)
						{
							if (atomA == atomB) continue;
							if (atomA.GetHashCode() > atomB.GetHashCode()) continue;
							ApplyRepelForce(atomA, atomB);
							ResolveCollision(atomA, atomB);
						}
					}
				}
			}
		}

		// --- Update atom positions and velocities ---
		UpdateAtomPositions(delta);

		// --- Reassign atoms to new chunks ---
		var newChunks = new Dictionary<ChunkCoord, AtomHolder>();
		foreach (var chunkAtoms in chunks.Values)
		{
			foreach (var atom in chunkAtoms)
			{
				var newChunk = GetChunkCoord(atom.position);
				if (!newChunks.ContainsKey(newChunk))
					newChunks[newChunk] = new AtomHolder();
				newChunks[newChunk].Add(atom);
			}
		}
		chunks = newChunks;
	}

	// --- PRIVATE HELPERS ---
	private float GetMeanBondLength(AtomInstance atomA, AtomInstance atomB)
	{
		return (atomA.r_e + atomB.r_e) * 0.5f;
	}

	public bool BreakBond(AtomInstance atomA, AtomInstance atomB)
	{
		return atomA.bonds.Remove(atomB) || atomB.bonds.Remove(atomA);
	}

	private void ApplyBondForce(AtomInstance atomA, AtomInstance atomB)
	{
		var pos1 = atomA.position;
		var pos2 = atomB.position;
		var rVec = pos2 - pos1;
		float r = rVec.Length();
		if (r == 0) return;
		var direction = rVec.Normalized();

		float a = atomA.a;
		float D_e = atomA.D_e;
		float r_e = atomA.r_e;
		float expTerm = Mathf.Exp(-a * (r - r_e));
		float forceMag = 2 * a * D_e * (1 - expTerm) * expTerm;

		if (r > r_e * 1.5f)
			forceMag += (r - r_e) * (atomA.extended_modifier + atomB.extended_modifier) * 0.5f;

		var force = direction * forceMag;

		var relativeVelocity = atomB.velocity - atomA.velocity;
		float bondDamping = 0.5f; // You may want to expose this as a parameter
		var dampingForce = direction * relativeVelocity.Dot(direction) * bondDamping;

		atomA.velocity += (dampingForce + force) / atomA.mass;
		atomB.velocity -= (dampingForce + force) / atomB.mass;
	}

	private void ApplyRepelForce(AtomInstance atomA, AtomInstance atomB)
	{
		var rVec = atomB.position - atomA.position;
		float r = rVec.Length();
		if (r == 0) return;
		var direction = rVec.Normalized();
		float k = 1000.0f;
		float q1 = atomA.charge;
		float q2 = atomB.charge;
		float forceMag = k * q1 * q2 / (r * r);
		if (forceMag <= 0) return;
		var force = direction * forceMag;
		atomB.velocity += force / atomB.mass;
		atomA.velocity -= force / atomA.mass;
	}

	private void ResolveCollision(AtomInstance atomA, AtomInstance atomB)
	{
		var delta = atomB.position - atomA.position;
		float dist = delta.Length();
		float minDist = atomA.radius + atomB.radius;

		if (dist == 0)
		{
			delta = new Vector2(GD.Randf(), GD.Randf()).Normalized();
			dist = 0.001f;
		}

		if (dist < minDist)
		{
			float overlap = minDist - dist;
			var direction = delta / dist;
			// Push each atom away half the overlap:
			atomA.position -= direction * overlap * 0.5f;
			atomB.position += direction * overlap * 0.5f;

			// Bounce effect (optional):
			var relativeVelocity = atomB.velocity - atomA.velocity;
			float velAlongNormal = relativeVelocity.Dot(direction);
			if (velAlongNormal > 0) return;

			float restitution = 0.8f;
			float impulse = -(1 + restitution) * velAlongNormal / 2.0f;
			var impulseVec = direction * impulse;

			atomA.velocity -= impulseVec;
			atomB.velocity += impulseVec;
		}
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
			return false; // Check if both atoms are valid and not already bonded
		}
		if (atomA.bonds.Contains(atomB) || atomB.bonds.Contains(atomA))
		{
			return false; // Bond already exists
		}
		if (GetMeanBondLength(atomA, atomB) * breakBondDistance <= Distance(atomA, atomB))
		{
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
