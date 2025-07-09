extends Node2D
class_name AtomPhysics

## --- EXPORTS ---
@export var start_atom: PackedScene
@export var player_input: AtomPlayerInput
@export var max_bond_dist_mult: float = 2.0
@export var chunk_size: int = 150
@export var break_bond_distance: float = 3.0
@export var atom_damping: float = 0.95
@export var bond_damping: float = 0.5

## --- FIELDS ---
var bonds: Dictionary[Atom, Array] = {}
var chunks: Dictionary[Vector2i, Array] = {}

## --- ENTRY POINT ---
func _ready():
	spawn_start()

## --- ACTION FUNCTIONS ---
func spawn_start():
	var start1 = spawn_atom(Vector2(350, 200))
	var start2 = spawn_atom(Vector2(470, 200))
	var start3 = spawn_atom(Vector2(410, 270))

	_add_bond(start1, start2)
	_add_bond(start1, start3)
	_add_bond(start2, start3)

func spawn_atom(pos: Vector2, bondTo: Atom = null) -> Atom:
	if bondTo and not _can_bond_single(bondTo):
		return null
	
	var atm = start_atom.instantiate() as Atom
	atm.atomClicked = player_input.atomClicked
	atm.atomHovered = player_input.atomHovered
	atm.position = pos
	add_child(atm)

	var ch = _calculate_atom_chunk(atm)
	if not chunks.has(ch):
		chunks[ch] = []
	chunks[ch].append(atm)

	if bondTo: 
		try_add_bond(atm, bondTo)
	return atm

func try_add_bond(atom_a: Atom, atom_b: Atom) -> bool:
	var dist = (atom_a.position - atom_b.position).length()
	var maxdist = _get_mean_bond_length(atom_a, atom_b) * max_bond_dist_mult
	if dist > maxdist:
		return false
	if not _can_bond(atom_a, atom_b):
		return false
	_add_bond(atom_a, atom_b)
	return true

func break_bond(atom_a: Atom, atom_b: Atom):
	bonds[atom_a].erase(atom_b)
	bonds[atom_b].erase(atom_a)

func is_bonded(atom_a: Atom, atom_b: Atom) -> bool:
	return bonds.has(atom_a) and bonds[atom_a].has(atom_b)

## --- PHYSICS PROCESSING ---
func _physics_process(_delta):
	var processed_pairs := {}

	# Bond processing
	for atom_a in bonds.keys():
		for atom_b in bonds[atom_a]:
			var pair_key = _get_pair_key(atom_a, atom_b)
			if processed_pairs.has(pair_key):
				continue
			processed_pairs[pair_key] = true

			var dist = (atom_a.position - atom_b.position).length()
			if dist > _get_mean_bond_length(atom_a, atom_b) * break_bond_distance:
				break_bond(atom_a, atom_b)
			else:
				_apply_bond_force(atom_a, atom_b)

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
						var pair_key = _get_pair_key(atom_a, atom_b)
						if processed_pairs.has(pair_key):
							continue
						processed_pairs[pair_key] = true
						_apply_repel_force(atom_a, atom_b)
						_resolve_collision(atom_a, atom_b)
	
	_update_atom_positions(_delta)
	
	# Reassign atoms to new chunks
	var new_chunks: Dictionary[Vector2i, Array] = {}
	for chunk_atoms in chunks.values():
		for atom in chunk_atoms:
			var new_chunk = _calculate_atom_chunk(atom)
			if not new_chunks.has(new_chunk):
				new_chunks[new_chunk] = []
			new_chunks[new_chunk].append(atom)
	chunks = new_chunks

	queue_redraw()

## --- DRAWING ---
func _draw():
	var drawn_pairs := {}
	for atom_a in bonds.keys():
		for atom_b in bonds[atom_a]:
			var pair_key = _get_pair_key(atom_a, atom_b)
			if drawn_pairs.has(pair_key):
				continue
			drawn_pairs[pair_key] = true
			draw_line(atom_a.position, atom_b.position, Color.WHITE, 5.0)

## --- PRIVATE HELPERS ---
func _add_bond(atom_a: Atom, atom_b: Atom) -> bool:
	if not bonds.has(atom_a):
		bonds[atom_a] = []
	if not bonds.has(atom_b):
		bonds[atom_b] = []
	if bonds[atom_a].has(atom_b) or bonds[atom_b].has(atom_a):
		return false
	bonds[atom_a].append(atom_b)
	bonds[atom_b].append(atom_a)
	return true

func _get_mean_bond_length(atom_a: Atom, atom_b: Atom) -> float:
	return (atom_a.get_equilibrum_bond_length() + atom_b.get_equilibrum_bond_length()) * 0.5

func _can_bond_single(atom_a: Atom) -> bool:
	var a_conns = bonds[atom_a].size() if bonds.has(atom_a) else 0
	return a_conns < atom_a.max_connections

func _can_bond(atom_a: Atom, atom_b: Atom) -> bool:
	var a_conns = bonds[atom_a].size() if bonds.has(atom_a) else 0
	var b_conns = bonds[atom_b].size() if bonds.has(atom_b) else 0
	return a_conns < atom_a.max_connections and b_conns < atom_b.max_connections

func _calculate_atom_chunk(atom: Atom) -> Vector2i:
	return Vector2i(atom.position / chunk_size)

func _get_pair_key(atom_a: Atom, atom_b: Atom) -> String:
	var id_a = atom_a.get_instance_id()
	var id_b = atom_b.get_instance_id()
	return str(min(id_a, id_b)) + ":" + str(max(id_a, id_b))

func _apply_bond_force(atom_a: Atom, atom_b: Atom):
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

	var relative_velocity = atom_b.linear_velocity - atom_a.linear_velocity
	var damping_force = direction * relative_velocity.dot(direction) * bond_damping

	atom_a.apply_central_force(damping_force+force)
	atom_b.apply_central_force(-damping_force-force)

func _apply_repel_force(atom_a: Atom, atom_b: Atom):
	var r_vec = atom_b.position - atom_a.position
	var r = r_vec.length()
	if r == 0:
		return

	var direction = r_vec.normalized()
	var k = 1000.0
	var q1 = atom_a.charge
	var q2 = atom_b.charge
	var force_mag = k * q1 * q2 / (r * r)

	if force_mag <= 0:
		return

	var force = direction * force_mag
	atom_b.apply_central_force(force)
	atom_a.apply_central_force(-force)

func _resolve_collision(atom_a: Atom, atom_b: Atom) -> void:
	var delta = atom_b.position - atom_a.position
	var dist = delta.length()
	var min_dist = atom_a.radius + atom_b.radius

	if dist == 0:
		delta = Vector2(randf(), randf()).normalized()
		dist = 0.001

	if dist < min_dist:
		var overlap = min_dist - dist
		var direction = delta / dist
		# Push each atom away half the overlap:
		atom_a.position -= direction * overlap * 0.5
		atom_b.position += direction * overlap * 0.5

		# Bounce effect (optional):
		var relative_velocity = atom_b.linear_velocity - atom_a.linear_velocity
		var vel_along_normal = relative_velocity.dot(direction)
		if vel_along_normal > 0:
			return

		var restitution = 0.8
		var impulse = (-(1 + restitution) * vel_along_normal) / 2.0
		var impulse_vec = direction * impulse

		atom_a.linear_velocity -= impulse_vec
		atom_b.linear_velocity += impulse_vec

func _update_atom_positions(delta: float) -> void:
	for chunk_atoms in chunks.values():
		for atom in chunk_atoms:
			# Simple Euler integration:
			atom.position += atom.linear_velocity * delta

			# Optional: Add damping to slow velocity a bit
			atom.linear_velocity *= atom_damping
