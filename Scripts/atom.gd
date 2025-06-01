extends RigidBody2D
class_name Atom

@export var nr_connections: int
@export var D_e: int       # Potential well depth
@export var a: float      # Width of the well
@export var r_e: float   # Equilibrium bond length

func get_well_width():
	return a

func get_potential_well_depth():
	return D_e

func get_equilibrum_bond_length():
	return r_e
