#extends RigidBody2D
extends Node2D
class_name Atom

@export var max_connections: int = 4
@export var D_e: int = 350       # Potential well depth
@export var a: float = 0.05      # Width of the well
@export var r_e: float = 100.0   # Equilibrium bond length
@export var extended_modifier: float = 0.2
@export var charge: float = 10

@export var mass: float = 12

var radius: float = 0

func _ready():
	radius = $Area2D/CollisionShape2D.get_shape().radius

var linear_velocity: Vector2 = Vector2.ZERO
func apply_central_force(force: Vector2) -> void:
	var acceleration = force / mass
	linear_velocity += acceleration

var atomClicked: Callable
var atomHovered: Callable

func get_well_width():
	return a

func get_potential_well_depth():
	return D_e

func get_equilibrum_bond_length():
	return r_e
