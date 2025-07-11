using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

using AtomNode = Godot.Node2D;
public partial class AtomSimulator : Node
{
	[Export] public Vector2 ChunkSize = new Vector2(400, 400);
	[Export] public float BreakBondDistance = 3f;
	PackedScene carbonAtomScene;


	Dictionary<string, AtomNode> atoms = new Dictionary<string, AtomNode>();
	AtomPhysics atomPhysics;

	public AtomSimulator()
	{
		atomHovered = new Callable(this, nameof(OnAtomHovered));
		atomClicked = new Callable(this, nameof(OnAtomClicked));
		carbonAtomScene = ResourceLoader.Load<PackedScene>("res://Atoms/Carbon/carbon_atom.tscn");
		atomPhysics = new AtomPhysics(ChunkSize, BreakBondDistance);
	}

	public override void _Ready()
	{
		var atomNode1 = (AtomNode)carbonAtomScene.Instantiate();
		AddChild(atomNode1);
		atomNode1.Position = new Vector2(350, 200);

		var atomNode2 = (AtomNode)carbonAtomScene.Instantiate();
		AddChild(atomNode2);
		atomNode2.Position = new Vector2(470, 200);

		var atomNode3 = (AtomNode)carbonAtomScene.Instantiate();
		AddChild(atomNode3);
		atomNode3.Position = new Vector2(410, 270);

		
		var atom1 = CreateAtomInstance(atomNode1);
		var atom2 = CreateAtomInstance(atomNode2);
		var atom3 = CreateAtomInstance(atomNode3);
		atomPhysics.AddAtomToChunk(atom1);
		atomPhysics.AddAtomToChunk(atom2);
		atomPhysics.AddAtomToChunk(atom3);

		atomPhysics.AddBond(atom1, atom2);
		atomPhysics.AddBond(atom1, atom3);
		atomPhysics.AddBond(atom2, atom3);

		foreach (var chHolder in atomPhysics.chunks.Values)
		{
			foreach (var atm in chHolder)
			{
				GD.Print($"Atom {atm.maxConnections}");
			}
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		var startTime = Time.GetTicksUsec();
		atomPhysics.Simulate((float)delta);
		var endTime = Time.GetTicksUsec();
		GD.Print($"Physics step took {(endTime - startTime) / 1000.0} ms");
	}

	public override void _Process(double delta)
	{
		foreach (var chunk in atomPhysics.chunks.Values)
		{
			foreach (var atom in chunk)
			{
				// Update the position of the atom's node
				if (atom.node != null)
				{
					atom.node.Position = atom.position;
				}
			}
		}
	}




	public Callable atomHovered;
	void OnAtomHovered(AtomNode atom)
	{
		
	}

	public Callable atomClicked;
	void OnAtomClicked(AtomNode atom)
	{
		DeleteAtom(atom);
	}

	void DeleteAtom(AtomNode atom)
	{
		var atm = atomPhysics.GetAtomByNode(atom);
		if (atm != null)
		{
			atomPhysics.RemoveAtom(atm);
		}
	}

	AtomInstance CreateAtomInstance(AtomNode atomNode)
	{
		atomNode.Set("atomHovered", atomHovered);
		atomNode.Set("atomClicked", atomClicked);
		var inst = new AtomInstance
		{
			position = atomNode.Position,
			velocity = new Vector2(0, 0),
			maxConnections = atomNode.Get("max_connections").As<int>(),
			D_e = atomNode.Get("D_e").As<float>(),
			a = atomNode.Get("a").As<float>(),
			r_e = atomNode.Get("r_e").As<float>(),
			extended_modifier = atomNode.Get("extended_modifier").As<float>(),
			charge = atomNode.Get("charge").As<float>(),
			mass = atomNode.Get("mass").As<float>(),
			radius = atomNode.Get("radius").As<float>(),
			node = atomNode,
			bonds = new List<AtomInstance>(),
		};
		return inst;
	}
}
