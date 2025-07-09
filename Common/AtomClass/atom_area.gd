extends Area2D

var atom: Atom

func _ready():
	input_pickable = true  # Enable mouse input picking
	atom = get_parent()

func _input_event(_viewport, event, _shape_idx):
	if event is InputEventMouseButton:
		if event.pressed and event.button_index == MOUSE_BUTTON_LEFT:
			atom.atomClicked.call(atom)
	elif event is InputEventMouseMotion:
		atom.atomHovered.call(atom)
