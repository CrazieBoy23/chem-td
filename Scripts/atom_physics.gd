extends Node2D
class_name AtomPhysics

@export var start_atom: PackedScene
@export var player_input: AtomPlayerInput
var bonds: Dictionary[Atom, Array] = {}
var chunks: Dictionary[Vector2i, Array] = {}

func _ready():
	spawn_start()

func get_mean_bond_length(atom_a: Atom, atom_b: Atom) -> float:
	return (atom_a.get_equilibrum_bond_length() + atom_b.get_equilibrum_bond_length()) * 0.5

@export var max_bond_dist_mult: float = 2.0
func try_add_bond(atom_a: Atom, atom_b: Atom) -> bool:
	var dist = (atom_a.position - atom_b.position).length()
	var maxdist = get_mean_bond_length(atom_a, atom_b) * max_bond_dist_mult
	if dist > maxdist:
		return false
	if bonds[atom_a].size() >= atom_a.max_connections or bonds[atom_b].size() >= atom_b.max_connections:
		return false
	add_bond(atom_a, atom_b)
	return true

func add_bond(atom_a: Atom, atom_b: Atom) -> bool:
	if not bonds.has(atom_a):
		bonds[atom_a] = []
	if not bonds.has(atom_b):
		bonds[atom_b] = []
	if bonds[atom_a].has(atom_b) or bonds[atom_b].has(atom_a):
		return false
	bonds[atom_a].append(atom_b)
	bonds[atom_b].append(atom_a)
	return true

func is_bonded(atom_a: Atom, atom_b: Atom) -> bool:
	return bonds.has(atom_a) and bonds[atom_a].has(atom_b)

func break_bond(atom_a: Atom, atom_b: Atom):
	bonds[atom_a].erase(atom_b)
	bonds[atom_b].erase(atom_a)

func spawn_atom(pos: Vector2, bondTo: Atom = null) -> Atom:
	var atm = start_atom.instantiate() as Atom
	atm.atomClicked = player_input.atomClicked
	atm.atomHovered = player_input.atomHovered
	atm.position = pos
	add_child(atm)

	var ch = calculate_atom_chunk(atm)
	if not chunks.has(ch):
		chunks[ch] = []
	chunks[ch].append(atm)

	if bondTo:
		add_bond(atm, bondTo)
	return atm

func spawn_start():
	var start1 = spawn_atom(Vector2(350, 200))
	var start2 = spawn_atom(Vector2(470, 200))
	var start3 = spawn_atom(Vector2(410, 270))

	add_bond(start1, start2)
	add_bond(start1, start3)
	add_bond(start2, start3)

func calculate_atom_chunk(atom: Atom) -> Vector2i:
	return Vector2i(atom.position / 150)

func get_pair_key(atom_a: Atom, atom_b: Atom) -> String:
	var id_a = atom_a.get_instance_id()
	var id_b = atom_b.get_instance_id()
	return str(min(id_a, id_b)) + ":" + str(max(id_a, id_b))

@export var break_bond_distance: float = 3.0
func _physics_process(_delta):
	var processed_pairs := {}

	# Bond processing
	for atom_a in bonds.keys():
		for atom_b in bonds[atom_a]:
			var pair_key = get_pair_key(atom_a, atom_b)
			if processed_pairs.has(pair_key):
				continue
			processed_pairs[pair_key] = true

			var dist = (atom_a.position - atom_b.position).length()
			if dist > get_mean_bond_length(atom_a, atom_b) * break_bond_distance:
				break_bond(atom_a, atom_b)
			else:
				apply_bond_force(atom_a, atom_b)

	# Repel force (using 3x3 chunks)
	var all_chunk_keys = chunks.keys()
	for chunk_key in all_chunk_keys:
		var chunk_atoms = chunks[chunk_key]
		for i in range(chunk_atoms.size()):
			var atom_a = chunk_atoms[i]
			for offset_x in range(-1, 2):
				for offset_y in range(-1, 2):
					var neighbor_key = chunk_key + Vector2i(offset_x, offset_y)
					if not chunks.has(neighbor_key):
						continue
					var neighbor_atoms = chunks[neighbor_key]
					for atom_b in neighbor_atoms:
						if atom_a == atom_b:
							continue
						var pair_key = get_pair_key(atom_a, atom_b)
						if processed_pairs.has(pair_key):
							continue
						processed_pairs[pair_key] = true
						apply_repel_force(atom_a, atom_b)

	# Reassign atoms to new chunks
	var new_chunks: Dictionary[Vector2i, Array] = {}
	for chunk_atoms in chunks.values():
		for atom in chunk_atoms:
			var new_chunk = calculate_atom_chunk(atom)
			if not new_chunks.has(new_chunk):
				new_chunks[new_chunk] = []
			new_chunks[new_chunk].append(atom)
	chunks = new_chunks

	queue_redraw()

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

	if r > r_e * 1.5:
		force_mag += (r - r_e) * (atom_a.extended_modifier + atom_b.extended_modifier) * 0.5

	var force = direction * force_mag
	atom_b.apply_central_force(-force)
	atom_a.apply_central_force(force)

	const damping_coefficient = 0.5
	var relative_velocity = atom_b.linear_velocity - atom_a.linear_velocity
	var damping_force = direction * relative_velocity.dot(direction) * damping_coefficient

	atom_a.apply_central_force(damping_force)
	atom_b.apply_central_force(-damping_force)

func apply_repel_force(atom_a: Atom, atom_b: Atom):
	var r_vec = atom_b.position - atom_a.position
	var r = r_vec.length()
	if r == 0:
		return # avoid division by zero

	var direction = r_vec.normalized()

	# Coulomb's law: F = k * q1 * q2 / r^2
	# Let's define an arbitrary constant k for tuning force magnitude:
	var k = 1000.0

	# Get charges; assume atom_a.charge and atom_b.charge exist:
	var q1 = atom_a.charge
	var q2 = atom_b.charge

	# Calculate force magnitude:
	var force_mag = k * q1 * q2 / (r * r)

	# If charges have the same sign, force_mag is positive (repulsive)
	# If charges have opposite signs, force_mag is negative (attractive)
	# To keep repel force only, apply only if force_mag > 0 (same charges)
	if force_mag <= 0:
		return

	# Apply the force pushing them away from each other
	var force = direction * force_mag

	atom_b.apply_central_force(force)
	atom_a.apply_central_force(-force)


func _draw():
	var drawn_pairs := {}
	for atom_a in bonds.keys():
		for atom_b in bonds[atom_a]:
			var pair_key = get_pair_key(atom_a, atom_b)
			if drawn_pairs.has(pair_key):
				continue
			drawn_pairs[pair_key] = true
			draw_line(atom_a.position, atom_b.position, Color.WHITE, 5.0)
