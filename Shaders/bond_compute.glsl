#[compute]
#version 450
layout(local_size_x = 64) in;

struct Atom {
    vec2 position;
    vec2 velocity;
    float radius;
    float charge;
    float max_connections;
    float extended_modifier;
    float equilibrium_bond_length;
    float well_width;
    float potential_well_depth;
};

layout(std430, binding=0) buffer AtomsBuffer {
    Atom atoms[];
};

layout(std430, binding=1) buffer BondsBuffer {
    ivec2 bonds[];  // pairs of indices into atoms[]
};

uniform float delta;
uniform float break_bond_distance;

float expf(float x) { return exp(x); }

void apply_bond_force(inout Atom a, inout Atom b) {
    vec2 r_vec = b.position - a.position;
    float r = length(r_vec);
    if (r == 0.0) return;
    vec2 direction = r_vec / r;

    float a_param = a.well_width;
    float D_e = a.potential_well_depth;
    float r_e = a.equilibrium_bond_length;

    float exp_term = expf(-a_param * (r - r_e));
    float force_mag = 2.0 * a_param * D_e * (1.0 - exp_term) * exp_term;

    if (r > r_e * 1.5) {
        force_mag += (r - r_e) * 0.5 * (a.extended_modifier + b.extended_modifier);
    }

    vec2 force = direction * force_mag;

    float damping_coefficient = 0.5;
    vec2 relative_velocity = b.velocity - a.velocity;
    vec2 damping_force = direction * dot(relative_velocity, direction) * damping_coefficient;

    vec2 total_force_a = damping_force + force;
    vec2 total_force_b = -damping_force - force;

    a.velocity += total_force_a * delta;
    b.velocity += total_force_b * delta;
}

void main() {
    uint idx = gl_GlobalInvocationID.x;
    if (idx >= bonds.length()) {
        return;
    }

    ivec2 bond = bonds[idx];
    int a_idx = bond.x;
    int b_idx = bond.y;

    Atom a = atoms[a_idx];
    Atom b = atoms[b_idx];

    float dist = length(b.position - a.position);
    float mean_eq_length = 0.5 * (a.equilibrium_bond_length + b.equilibrium_bond_length);
    if (dist > mean_eq_length * break_bond_distance) {
        return; // bond too far, no force applied
    }

    apply_bond_force(a, b);

    atoms[a_idx] = a;
    atoms[b_idx] = b;
}
