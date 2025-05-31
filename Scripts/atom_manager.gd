extends Node2D
class_name AtomManager

@export var start_atom: PackedScene

func _ready():
	spawn_start()

func spawn_start():
	var start1 = start_atom.instantiate() as RigidBody2D
	var start2 = start_atom.instantiate() as RigidBody2D

	start1.position = Vector2(350, 200)
	start2.position = Vector2(400, 200)

	start1.freeze = true

	add_child(start1)
	add_child(start2)

	start2.apply_impulse(Vector2.ZERO, Vector2.RIGHT * 50) # Apply impulse at center

	var joint = DampedSpringJoint2D.new()
	joint.node_a = start1.get_path()
	joint.node_b = start2.get_path()
	joint.length = start1.position.distance_to(start2.position) # natural length
	joint.stiffness = 1.0
	joint.damping = 1
	joint.position = (start1.position + start2.position) / 2

	add_child(joint)
