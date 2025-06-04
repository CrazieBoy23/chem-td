extends RigidBody2D
class_name Atom

@export var max_connections: int = 4
@export var D_e: int = 350       # Potential well depth
@export var a: float = 0.05      # Width of the well
@export var r_e: float = 100.0   # Equilibrium bond length
@export var extended_modifier: float = 0.2
@export var charge: float = 10

# atomClicked(Atom)
var atomClicked: Callable
var atomHovered: Callable

func get_well_width():
	return a

func get_potential_well_depth():
	return D_e

func get_equilibrum_bond_length():
	return r_e

func _ready():
	input_pickable = true  # Enable mouse input picking

func _input_event(viewport, event, shape_idx):
	if event is InputEventMouseButton:
		if event.pressed and event.button_index == MOUSE_BUTTON_LEFT:
			atomClicked.call(self)
	elif event is InputEventMouseMotion:
		atomHovered.call(self)
