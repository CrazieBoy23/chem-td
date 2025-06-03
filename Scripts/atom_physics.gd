extends Node2D
class_name AtomPhysics

@export var start_atom: PackedScene
var bonds: Dictionary[Atom, Array] = {}

func _ready():
	spawn_start()

func add_bond(atom_a: Atom, atom_b: Atom):
	if not bonds.has(atom_a):
		bonds[atom_a] = []
	if not bonds.has(atom_b):
		bonds[atom_b] = []
	bonds[atom_a].append(atom_b)
	bonds[atom_b].append(atom_a)
	#lines.draw_line()

func spawn_start():
	var start1 = start_atom.instantiate() as Atom
	var start2 = start_atom.instantiate() as Atom
	var start3 = start_atom.instantiate() as Atom
	start1.id = 1
	start2.id = 2
	start3.id = 3

	start1.position = Vector2(350, 200)
	start2.position = Vector2(470, 200)
	start3.position = Vector2(410, 270)

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
			var id_a = atom_a.get_instance_id()
			var id_b = atom_b.get_instance_id()
			var pair_key = str(min(id_a, id_b)) + ":" + str(max(id_a, id_b))

			if processed_pairs.has(pair_key):
				continue
			processed_pairs[pair_key] = true

			apply_bond_force(atom_a, atom_b)
			
			# Optional: extra force to hold first atom
			if atom_a.id == 0:
				atom_a.apply_central_force(Vector2(-100, 0))

func apply_bond_force(atom_a: Atom, atom_b: Atom):
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

	# Slight boost at long distances to prevent atoms from drifting too far
	if r > r_e * 1.5:
		force_mag += (r - r_e) * (atom_a.extended_modifier + atom_b.extended_modifier)*0.5
	
	var force = direction * force_mag

	atom_b.apply_central_force(-force)
	atom_a.apply_central_force(force)
	
	const damping_coefficient = 0.5
	var relative_velocity = atom_b.linear_velocity - atom_a.linear_velocity
	var damping_force = direction * relative_velocity.dot(direction) * damping_coefficient

	atom_a.apply_central_force(damping_force)
	atom_b.apply_central_force(-damping_force)
	
	queue_redraw()

func _draw():
	var drawn_pairs := {}

	for atom_a in bonds.keys():
		for atom_b in bonds[atom_a]:
			var id_a = atom_a.get_instance_id()
			var id_b = atom_b.get_instance_id()
			var pair_key = str(min(id_a, id_b)) + ":" + str(max(id_a, id_b))

			if drawn_pairs.has(pair_key):
				continue
			drawn_pairs[pair_key] = true

			draw_line(atom_a.position, atom_b.position, Color.WHITE, 5.0)
