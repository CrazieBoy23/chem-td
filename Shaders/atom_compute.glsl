#version 450
#[compute]
layout(local_size_x = 64) in;

struct Atom {
    vec2 position;
    vec2 velocity;
    float radius;
    float charge;
    float well_width;
    float potential_depth;
    float equilibrium_length;
    float extended_modifier;
    int max_connections;
};

layout(std430, set = 0, binding = 0) buffer AtomBuffer {
    Atom atoms[];
};

layout(std430, set = 0, binding = 1) buffer BondsBuffer {
    ivec2 bonds[];
};

layout(std430, set = 0, binding = 2) buffer OutputBuffer {
    Atom output_atoms[];
};

uniform float break_bond_distance = 3.0;
uniform float damping_coefficient = 0.5;
uniform float repel_k = 1000.0;
uniform int atom_count;
uniform int bonds_count;
uniform float delta_time = 0.016; // timestep

// Calculate bond force for one bond
vec2 compute_bond_force(Atom atom, Atom other) {
    vec2 r_vec = other.position - atom.position;
    float r = length(r_vec);
    if (r == 0.0) return vec2(0.0);
    vec2 dir = r_vec / r;

    float a = atom.well_width;
    float D_e = atom.potential_depth;
    float r_e = atom.equilibrium_length;

    float exp_term = exp(-a * (r - r_e));
    float force_mag = 2.0 * a * D_e * (1.0 - exp_term) * exp_term;

    if (r > r_e * 1.5) {
        force_mag += (r - r_e) * (atom.extended_modifier + other.extended_modifier) * 0.5;
    }

    return dir * force_mag;
}

// Calculate damping force along bond direction
vec2 compute_damping_force(Atom atom, Atom other, vec2 dir) {
    vec2 relative_velocity = other.velocity - atom.velocity;
    return dir * dot(relative_velocity, dir) * damping_coefficient;
}

// Check if bond should break
bool should_break_bond(Atom atom, Atom other) {
    float r = length(other.position - atom.position);
    return (r > atom.equilibrium_length * break_bond_distance);
}

// Calculate repulsion force from another atom
vec2 compute_repulsion_force(Atom atom, Atom other) {
    vec2 r_vec = other.position - atom.position;
    float r = length(r_vec);
    if (r == 0.0) return vec2(0.0);
    vec2 dir = r_vec / r;

    float q1 = atom.charge;
    float q2 = other.charge;

    float force_mag = repel_k * q1 * q2 / (r * r);
    if (force_mag <= 0.0) return vec2(0.0);

    return -dir * force_mag;
}

// Resolve collisions between two atoms by adjusting position
vec2 resolve_collision(Atom atom, Atom other) {
    vec2 delta_pos = other.position - atom.position;
    float dist = length(delta_pos);
    float min_dist = atom.radius + other.radius;
    if (dist == 0.0 || dist >= min_dist) return vec2(0.0);
    vec2 dir = delta_pos / dist;
    float overlap = min_dist - dist;
    return -dir * (overlap * 0.5); // move atom away half overlap
}

void main() {
    uint i = gl_GlobalInvocationID.x;
    if (i >= atom_count) return;

    Atom atom = atoms[i];
    vec2 force = vec2(0.0);

    // Bond forces + damping
    for (int b = 0; b < bonds_count; b++) {
        ivec2 bond = bonds[b];
        if (bond.x == -1) continue;

        int other_idx = -1;
        if (bond.x == int(i)) other_idx = bond.y;
        else if (bond.y == int(i)) other_idx = bond.x;
        else continue;

        Atom other = atoms[other_idx];

        if (should_break_bond(atom, other)) {
            // bond broken, skip force or implement bond removal on CPU
            continue;
        }

        vec2 r_vec = other.position - atom.position;
        float r = length(r_vec);
        if (r == 0.0) continue;
        vec2 dir = r_vec / r;

        force += compute_bond_force(atom, other);
        force += compute_damping_force(atom, other, dir);
    }

    // Repulsion forces from all other atoms
    for (uint j = 0; j < atom_count; j++) {
        if (j == i) continue;
        Atom other = atoms[j];
        force += compute_repulsion_force(atom, other);
    }

    // Euler integration
    vec2 acceleration = force; // assume mass = 1
    atom.velocity += acceleration * delta_time;
    atom.position += atom.velocity * delta_time;

    // Collision resolution
    for (uint j = 0; j < atom_count; j++) {
        if (j == i) continue;
        Atom other = atoms[j];
        vec2 correction = resolve_collision(atom, other);
        atom.position += correction;
    }

    output_atoms[i] = atom;
}
