extends Node
class_name AtomPlayerInput

@export var atom_physics: AtomPhysics

enum ClickedState
{
	NOTCLICKED,
	CLICKED,
	EXTENDED,
}
var state: ClickedState = ClickedState.NOTCLICKED
var clicked_atom: Atom = null
var mouse_pos: Vector2

func atomClicked(clicked: Atom):
	state = ClickedState.CLICKED
	clicked_atom = clicked

func _unhandled_input(event):
	if event is InputEventMouseMotion:
		mouse_pos = event.position
	if event is InputEventMouseButton:
		if not event.pressed:
			if state!=ClickedState.NOTCLICKED:
				if state==ClickedState.EXTENDED:
					spawn_atom()
				state = ClickedState.NOTCLICKED
				clicked_atom = null

func spawn_atom():
	var delta_mouse = mouse_pos - clicked_atom.position
	delta_mouse = delta_mouse.normalized()
	delta_mouse = delta_mouse*clicked_atom.get_equilibrum_bond_length()
	var spawn_pos = clicked_atom.position+delta_mouse
	atom_physics.spawn_atom(spawn_pos, clicked_atom)

func _process(delta: float) -> void:
	if state == ClickedState.NOTCLICKED:
		return
	var delta_mouse = clicked_atom.position - mouse_pos
	var len_mouse = delta_mouse.length()
	if len_mouse > clicked_atom.get_equilibrum_bond_length()*0.5:
		state = ClickedState.EXTENDED
	else:
		state = ClickedState.CLICKED
