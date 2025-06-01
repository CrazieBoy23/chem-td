extends Node2D
class_name AtomManager

@export var start_atom: PackedScene
var atoms: Array[Atom]
var bonds: Dictionary = {}

# Morse potential parameters

func _ready():
	spawn_start()

func add_bond(atom_a: Atom, atom_b: Atom):
	if not bonds.has(atom_a):
		bonds[atom_a] = []
	if not bonds.has(atom_b):
		bonds[atom_b] = []
	bonds[atom_a].append(atom_b)
	bonds[atom_b].append(atom_a)

func spawn_start():
	var start1 = start_atom.instantiate() as Atom
	var start2 = start_atom.instantiate() as Atom
	var start3 = start_atom.instantiate() as Atom

	start1.position = Vector2(350, 200)
	start2.position = Vector2(470, 200)
	start3.position = Vector2(410, 270)

	atoms.append(start1)
	atoms.append(start2)
	atoms.append(start3)

	add_child(start1)
	add_child(start2)
	add_child(start3)
	
	add_bond(start1, start2)
	add_bond(start1, start3)
	add_bond(start2, start3)

func _physics_process(_delta):
	var processed_pairs := {}

	for atom_a in bonds.keys():
		for atom_b in bonds[atom_a]:
			# Avoid duplicate processing by ensuring we only process one direction
			var id_a = atom_a.get_instance_id()
			var id_b = atom_b.get_instance_id()
			var pair_key = str(min(id_a, id_b)) + ":" + str(max(id_a, id_b))

			if processed_pairs.has(pair_key):
				continue
			processed_pairs[pair_key] = true

			# Apply equal and opposite forces to both atoms
			apply_bond_force(atom_a, atom_b)
	print("-------")

func apply_bond_force(atom_a: Atom, atom_b: Atom):
		#Morse potential and derivative of the force calculations
	var pos1 = atom_a.position
	var pos2 = atom_b.position
	var r_vec = pos2 - pos1
	var r = r_vec.length()
	var direction = r_vec.normalized()
	var a = atom_a.get_well_width()
	var D_e = atom_a.get_potential_well_depth()
	var r_e = atom_a.get_equilibrum_bond_length()

	var exp_term = exp(-a * (r - r_e))
	var force_mag = 2 * a * D_e * (1 - exp_term) * exp_term

	var force = direction * force_mag

	atom_b.apply_central_force(-force)
	atom_a.apply_central_force(force)
	print(-force)
