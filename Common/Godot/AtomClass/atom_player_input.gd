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
var hovered_atom: Atom = null
var mouse_pos: Vector2

var is_bonded: Callable
var try_add_bond: Callable
var break_bond: Callable
var spawn_atom_call: Callable

func setup(_is_bonded: Callable, _try_add_bond: Callable, _break_bond: Callable, _spawn_atom_call: Callable):
	self.is_bonded = _is_bonded
	self.try_add_bond = _try_add_bond
	self.break_bond = _break_bond
	self.spawn_atom_call = _spawn_atom_call

func atomClicked(clicked: Atom):
	state = ClickedState.CLICKED
	clicked_atom = clicked

func atomHovered(hovered: Atom):
	hovered_atom = hovered

func _unhandled_input(event):
	if event is InputEventMouseMotion:
		mouse_pos = event.position
		hovered_atom = null
	if event is InputEventMouseButton:
		if not event.pressed:
			if state!=ClickedState.NOTCLICKED:
				if state==ClickedState.EXTENDED:
					handle_player_action()
				state = ClickedState.NOTCLICKED
				clicked_atom = null

func handle_player_action():
	if hovered_atom and hovered_atom!=clicked_atom:
		if is_bonded.call(hovered_atom, clicked_atom):
			break_bond.call(hovered_atom, clicked_atom)
		else:
			try_add_bond.call(hovered_atom, clicked_atom)
	else:
		spawn_atom()

func spawn_atom():
	var delta_mouse = mouse_pos - clicked_atom.position
	delta_mouse = delta_mouse.normalized()
	delta_mouse = delta_mouse*clicked_atom.r_e
	var spawn_pos = clicked_atom.position+delta_mouse
	spawn_atom_call.call(spawn_pos, clicked_atom)

func _process(_delta: float) -> void:
	if state == ClickedState.NOTCLICKED:
		return
	var delta_mouse = clicked_atom.position - mouse_pos
	var len_mouse = delta_mouse.length()
	if len_mouse > clicked_atom.r_e*0.5:
		state = ClickedState.EXTENDED
	else:
		state = ClickedState.CLICKED
