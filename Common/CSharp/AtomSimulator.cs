using Godot;
using System;
using System.Collections.Generic;

using Atom = Godot.Node2D;
public partial class AtomSimulator : Node
{
	[Export] public Vector2 ChunkSize = new Vector2(100, 100);
	Dictionary<string, Atom> atoms = new Dictionary<string, Atom>();

	

	public override void _Ready()
	{
		// var atomScript = GD.Load<GDScript>("res://Atoms/Carbon/carbon_atom.tscn");
		// var atomNode = (Atom)atomScript.New();
		atomHovered = new Callable(this, nameof(OnAtomHovered));
		atomClicked = new Callable(this, nameof(OnAtomClicked));

		var atomNode = (Atom)ResourceLoader.Load<PackedScene>("res://Atoms/Carbon/carbon_atom.tscn").Instantiate();
		AddChild(atomNode);
		atomNode.Position = new Vector2(500, 500);

		AtomPhysics ap = new AtomPhysics(ChunkSize);
		var atom = CreateAtomInstance(atomNode);
		ap.AddAtom(atom);

		foreach (var chHolder in ap.chunks.Values)
		{
			foreach (var atm in chHolder)
			{
				GD.Print($"Atom {atm.maxConnections}");
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
