extends Node2D
class_name AtomManager

@export var start_atom: PackedScene
var atoms: Array[Atom]

# Morse potential parameters
var D_e = 350       # Potential well depth
var a = 0.05      # Width of the well
var r_e = 100.0     # Equilibrium bond length

func _ready():
	spawn_start()

func spawn_start():
	var start1 = start_atom.instantiate() as Atom
	var start2 = start_atom.instantiate() as Atom

	start1.position = Vector2(350, 200)
	start2.position = Vector2(470, 200)

	atoms.append(start1)
	atoms.append(start2)

	add_child(start1)
	add_child(start2)

func _physics_process(delta):
	#Morse potential and derivative of the force calculations
	var pos1 = atoms[0].position
	var pos2 = atoms[1].position
	var r_vec = pos2 - pos1
	var r = r_vec.length()
	var direction = r_vec.normalized()

	var exp_term = exp(-a * (r - r_e))
	var force_mag = 2 * a * D_e * (1 - exp_term) * exp_term

	var force = direction * force_mag
	print(exp_term, "  ", force_mag, force)

	atoms[0].apply_central_force(force)
	atoms[1].apply_central_force(-force)
