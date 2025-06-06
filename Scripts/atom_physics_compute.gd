extends Node

@export var atom_count: int
@export var bond_count: int

@export var compute_bond_shader: Shader
@export var compute_atom_shader: Shader

var atoms_buffer: StorageBuffer
var bonds_buffer: StorageBuffer

var compute_bond: ComputeShader
var compute_atom: ComputeShader

var chunk_size := 150.0
var chunk_grid_size := Vector2i(10, 10) # adjust to your world size

func _ready():
	# Create buffers
	atoms_buffer = StorageBuffer.new()
	bonds_buffer = StorageBuffer.new()

	atoms_buffer.resize(atom_count * sizeof_atom())
	bonds_buffer.resize(bond_count * sizeof_bond())

	# Initialize atoms data
	var atoms_data = PoolByteArray()
	atoms_data.resize(atom_count * sizeof_atom())
	# Fill atoms_data here with your atom properties and initial positions/velocities
	# Example: set random positions, zero velocities, radius=5, charge=1, etc.
	initialize_atoms_data(atoms_data)
	atoms_buffer.set_data(atoms_data)

	# Initialize bonds data
	var bonds_data = PoolByteArray()
	bonds_data.resize(bond_count * sizeof_bond())
	# Fill bonds_data here with atom index pairs of bonds
	initialize_bonds_data(bonds_data)
	bonds_buffer.set_data(bonds_data)

	# Setup compute shaders
	compute_bond = ComputeShader.new()
	compute_bond.shader = compute_bond_shader
	compute_bond.set_storage_buffer(0, atoms_buffer)
	compute_bond.set_storage_buffer(1, bonds_buffer)

	compute_atom = ComputeShader.new()
	compute_atom.shader = compute_atom_shader
	compute_atom.set_storage_buffer(0, atoms_buffer)

func sizeof_atom() -> int:
	# vec2 pos(8 bytes) + vec2 vel(8) + 7 floats(28 bytes) = 44 bytes approx, align to 48
	# Use 48 bytes for simplicity (std430 rules)
	return 48

func sizeof_bond() -> int:
	# ivec2 = 8 bytes
	return 8

func initialize_atoms_data(data: PoolByteArray) -> void:
	# Write atom structs into data
	var buf = data.write()
	for i in range(atom_count):
		# position vec2 (float32)
		var x = randf() * chunk_grid_size.x * chunk_size
		var y = randf() * chunk_grid_size.y * chunk_size
		buf.store_float(i * sizeof_atom() + 0, x)
		buf.store_float(i * sizeof_atom() + 4, y)
		# velocity vec2 zero
		buf.store_float(i * sizeof_atom() + 8, 0.0)
		buf.store_float(i * sizeof_atom() + 12, 0.0)
		# radius
		buf.store_float(i * sizeof_atom() + 16, 5.0)
		# charge
		buf.store_float(i * sizeof_atom() + 20, 1.0)
		# max_connections (unused)
		buf.store_float(i * sizeof_atom() + 24, 4.0)
		# extended_modifier
		buf.store_float(i * sizeof_atom() + 28, 0.0)
		# equilibrium_bond_length
		buf.store_float(i * sizeof_atom() + 32, 20.0)
		# well_width
		buf.store_float(i * sizeof_atom() + 36, 1.0)
		# potential_well_depth
		buf.store_float(i * sizeof_atom() + 40, 1.0)
	buf.release()

func initialize_bonds_data(data: PoolByteArray) -> void:
	var buf = data.write()
	# For testing, create some bonds randomly
	for i in range(bond_count):
		var a = randi() % atom_count
		var b = randi() % atom_count
		if a == b:
			b = (b + 1) % atom_count
		buf.store_32(i * sizeof_bond() + 0, a)
		buf.store_32(i * sizeof_bond() + 4, b)
	buf.release()

func _process(delta: float) -> void:
	# Run bond shader
	compute_bond.set_uniform("delta", delta)
	compute_bond.set_uniform("break_bond_distance", 3.0)
	compute_bond.dispatch( (bond_count + 63) / 64 )

	# Run atom shader
	compute_atom.set_uniform("delta", delta)
	compute_atom.set_uniform("atom_count", atom_count)
	compute_atom.set_uniform("chunk_size", chunk_size)
	compute_atom.set_uniform("chunk_grid_size", chunk_grid_size)
	compute_atom.dispatch( (atom_count + 63) / 64 )

	# Optional: read back atoms_buffer if needed for CPU-side logic
