using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using AtomNode = Godot.Node2D;
using Vector2 = Godot.Vector2;
public partial class AtomSimulator : Node
{
	[Export] public Node AtomPlayerInput;
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


	public bool IsBonded(AtomNode atom1n, AtomNode atom2n)
	{
		var atom1 = atomPhysics.GetAtomByNode(atom1n);
		var atom2 = atomPhysics.GetAtomByNode(atom2n);
		return atomPhysics.IsBonded(atom1, atom2);
	}
	public bool TryAddBond(AtomNode atom1n, AtomNode atom2n)
	{
		var atom1 = atomPhysics.GetAtomByNode(atom1n);
		var atom2 = atomPhysics.GetAtomByNode(atom2n);
		return atomPhysics.AddBond(atom1, atom2);
	}
	public bool BreakBond(AtomNode atom1n, AtomNode atom2n)
	{
		var atom1 = atomPhysics.GetAtomByNode(atom1n);
		var atom2 = atomPhysics.GetAtomByNode(atom2n);
		return atomPhysics.BreakBond(atom1, atom2);
	}

	public bool SpawnAtom(Vector2 spawnPos, AtomNode clickedAtom)
	{
		var inst = CreateAtomInstance(carbonAtomScene, spawnPos);
		atomPhysics.AddBond(atomPhysics.GetAtomByNode(clickedAtom), inst);

		return true;
	}

	public override void _Ready()
	{
		AtomPlayerInput.Call("setup",
			new Callable(this, nameof(IsBonded)),
			new Callable(this, nameof(TryAddBond)),
			new Callable(this, nameof(BreakBond)),
			new Callable(this, nameof(SpawnAtom)));

		var atom1 = CreateAtomInstance(carbonAtomScene, new Vector2(350, 200));
		var atom2 = CreateAtomInstance(carbonAtomScene, new Vector2(470, 200));
		var atom3 = CreateAtomInstance(carbonAtomScene, new Vector2(410, 270));

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
		AtomPlayerInput.Call("atomHovered", atom);
	}

	public Callable atomClicked;
	void OnAtomClicked(AtomNode atom)
	{
		AtomPlayerInput.Call("atomClicked", atom);
	}

	AtomInstance CreateAtomInstance(PackedScene atomScene, Vector2 position)
	{
		var atomNode = (AtomNode)atomScene.Instantiate();
		atomNode.Position = position;
		atomNode.Set("atomHovered", atomHovered);
		atomNode.Set("atomClicked", atomClicked);
		AddChild(atomNode);
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
		atomPhysics.AddAtomToChunk(inst);
		return inst;
	}
}
