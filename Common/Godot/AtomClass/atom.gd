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

var atomClicked: Callable
var atomHovered: Callable
