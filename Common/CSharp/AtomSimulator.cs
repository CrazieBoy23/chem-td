using Godot;
using System;
using System.Collections.Generic;

using Atom = Godot.Node2D;
public partial class AtomSimulator : Node
{
	[Export] public Vector2 ChunkSize = new Vector2(400, 400);
	PackedScene carbonAtomScene;


	Dictionary<string, Atom> atoms = new Dictionary<string, Atom>();
	AtomPhysics atomPhysics;

	public AtomSimulator()
	{
		atomHovered = new Callable(this, nameof(OnAtomHovered));
		atomClicked = new Callable(this, nameof(OnAtomClicked));
		carbonAtomScene = ResourceLoader.Load<PackedScene>("res://Atoms/Carbon/carbon_atom.tscn");
		atomPhysics = new AtomPhysics(ChunkSize);
	}

	public override void _Ready()
	{
		var atomNode = (Atom)carbonAtomScene.Instantiate();
		AddChild(atomNode);
		atomNode.Position = new Vector2(500, 0);

		
		var atom = CreateAtomInstance(atomNode);
		atomPhysics.AddAtomToChunk(atom);

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
		atomPhysics.Simulate((float)delta);
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
	void OnAtomHovered(Atom atom)
	{
		
	}

	public Callable atomClicked;
	void OnAtomClicked(Atom atom)
	{
		
	}

	AtomInstance CreateAtomInstance(Atom atomNode)
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
			node = atomNode
		};
		return inst;
	}
}
