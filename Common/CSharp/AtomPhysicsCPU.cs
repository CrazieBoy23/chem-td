using Godot;
using System;
using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Linq;

using ChunkCoord = Godot.Vector2I;
using AtomHolder = System.Collections.Generic.List<AtomInstance>;

public static class AtomPhysicsCPU
{
    public static void Simulate(Dictionary<ChunkCoord, AtomHolder> chunks)
    {
        // --- Bond Processing ---
		foreach (var kvp in chunks)
		{
			foreach (var atomA in kvp.Value.ToList())
			{
				foreach (var atomB in atomA.bonds.ToList())
				{
					if (atomA.GetHashCode() > atomB.GetHashCode()) continue; // Ensure each pair is processed only once
                    ApplyBondForce(atomA, atomB);
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
	}

    private static void ApplyBondForce(AtomInstance atomA, AtomInstance atomB)
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


    private static void ApplyRepelForce(AtomInstance atomA, AtomInstance atomB)
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


    private static void ResolveCollision(AtomInstance atomA, AtomInstance atomB)
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
    

    private static float GetMeanBondLength(AtomInstance atomA, AtomInstance atomB)
	{
		return (atomA.r_e + atomB.r_e) * 0.5f;
	}
}
