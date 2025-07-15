#[compute]
#version 450

layout(local_size_x = 64, local_size_y = 1, local_size_z = 1) in;

layout(set = 0, binding = 0, std430) restrict buffer AtomBuffer {
    float atom_data[];
};

layout(set = 0, binding = 1, std430) restrict buffer AdditionalBuffer {
    float additional_data[];
};

layout(set = 0, binding = 2, std430) restrict buffer ChunkInfoBuffer {
    int chunk_info[];
};

layout(set = 0, binding = 3, std430) restrict buffer BondIndicesBuffer {
    int bond_indices[];
};
layout(set = 0, binding = 4, std430) restrict buffer BondOffsetBuffer {
    int bond_offset[];
};
layout(set = 0, binding = 5, std430) restrict buffer BondCountBuffer {
    int bond_count[];
};

// Helper to get atom data
void get_atom(uint idx, out float pos_x, out float pos_y, out float vel_x, out float vel_y, out float mass, out float charge, out float radius) {
    uint base = idx * 8;
    pos_x = atom_data[base + 0];
    pos_y = atom_data[base + 1];
    vel_x = atom_data[base + 2];
    vel_y = atom_data[base + 3];
    mass  = atom_data[base + 4];
    charge = atom_data[base + 5];
    radius = atom_data[base + 6];
}

// Helper to set atom data
void set_atom(uint idx, float pos_x, float pos_y, float vel_x, float vel_y) {
    uint base = idx * 8;
    atom_data[base + 0] = pos_x;
    atom_data[base + 1] = pos_y;
    atom_data[base + 2] = vel_x;
    atom_data[base + 3] = vel_y;
}

void get_mean_atributes(uint idxa, uint idxb, out float D_e, out float a, out float r_e, out float extended_modifier) {
    float D_e1 = additional_data[idxa * 4 + 0];
    float a1 = additional_data[idxa * 4 + 1];
    float r_e1 = additional_data[idxa * 4 + 2];
    float extended_modifier1 = additional_data[idxa * 4 + 3];

    float D_e2 = additional_data[idxb * 4 + 0];
    float a2 = additional_data[idxb * 4 + 1];
    float r_e2 = additional_data[idxb * 4 + 2];
    float extended_modifier2 = additional_data[idxb * 4 + 3];

    // Calculate mean attributes
    D_e = (D_e1 + D_e2) / 2.0;
    a = (a1 + a2) / 2.0;
    r_e = (r_e1 + r_e2) / 2.0;
    extended_modifier = (extended_modifier1 + extended_modifier2) / 2.0;
}

float random() {
    // Simple random number generator
    return fract(sin(gl_GlobalInvocationID.x * 12.9898 + gl_GlobalInvocationID.y * 78.233) * 43758.5453);
}

void main() {
    uint atom_index = gl_GlobalInvocationID.x;
    uint base = atom_index * 8;
    float pos_x = atom_data[base + 0];
    float pos_y = atom_data[base + 1];
    float vel_x = atom_data[base + 2];
    float vel_y = atom_data[base + 3];
    float mass  = atom_data[base + 4];
    float charge = atom_data[base + 5];
    float radius = atom_data[base + 6];

    float D_e = additional_data[0]; 
    float a = additional_data[1];
    float r_e = additional_data[2];
    float extended_modifier = additional_data[3];

    vec2 pos = vec2(pos_x, pos_y);
    vec2 vel = vec2(vel_x, vel_y);

    // Apply bond forces
    int bond_start = bond_offset[atom_index];
    int bond_count = bond_count[atom_index];
    for (int i = 0; i < bond_count; ++i) {
        int bond_idx = bond_indices[bond_start + i];
        if (bond_idx < 0) continue;

        // Get bonded atom data
        float b_pos_x, b_pos_y, b_vel_x, b_vel_y, b_mass, b_charge, b_radius;
        get_atom(uint(bond_idx), b_pos_x, b_pos_y, b_vel_x, b_vel_y, b_mass, b_charge, b_radius);

        // Calculate bond attributes
        float bond_D_e, bond_a, bond_r_e, bond_extended_modifier;
        get_mean_atributes(atom_index, uint(bond_idx), bond_D_e, bond_a, bond_r_e, bond_extended_modifier);

        // Calculate bond force
        vec2 b_pos = vec2(b_pos_x, b_pos_y);
        vec2 b_vel = vec2(b_vel_x, b_vel_y);
        vec2 r_vec = b_pos - pos;
        float r = length(r_vec);
        vec2 r_dir = normalize(r_vec);

        float exp_term = exp(-a * (r - r_e));
        float force_mag = 2 * a * D_e * exp_term * (1 - exp_term);

        if (r > r_e * 1.5) {
            force_mag += (r - r_e) * bond_extended_modifier;
        }

        // Apply force
        vec2 force = r_dir * force_mag;
        vel += force / mass; // Update velocity based on force
    }

    // Find which chunk this atom belongs to
    int my_chunk = -1;
    int num_chunks = chunk_info[0];
    for (int c = 1; c <= num_chunks; ++c) {
        int start = chunk_info[c * 2 + 1];
        int count = chunk_info[c * 2];
        if (atom_index >= uint(start) && atom_index < uint(start + count)) {
            my_chunk = c;
            break;
        }
    }
    if (my_chunk == -1) return;

    int chunk_count = chunk_info[0];
    int chunk_rows = chunk_info[1];
    int chunk_cols = chunk_count / chunk_rows;

    // Figure out my chunk coordinates (X, Y)
    int my_chunk_x = (my_chunk - 1) % chunk_cols;
    int my_chunk_y = (my_chunk - 1) / chunk_cols;

    // 3x3 neighbor loop
    for (int dy = -1; dy <= 1; ++dy) {
        for (int dx = -1; dx <= 1; ++dx) {
            int nx = my_chunk_x + dx;
            int ny = my_chunk_y + dy;

            if (nx < 0 || ny < 0 || nx >= chunk_cols || ny >= chunk_rows) continue;

            int neighbor_chunk = ny * chunk_cols + nx + 1;
            if (neighbor_chunk < 1 || neighbor_chunk > chunk_count) continue;

            int count = chunk_info[neighbor_chunk * 2];
            int start = chunk_info[neighbor_chunk * 2 + 1];

            for (int j = 0; j < count; ++j) {
                uint other_idx = uint(start + j);
                if (other_idx == atom_index) continue;

                float o_pos_x, o_pos_y, o_vel_x, o_vel_y, o_mass, o_charge, o_radius;
                get_atom(other_idx, o_pos_x, o_pos_y, o_vel_x, o_vel_y, o_mass, o_charge, o_radius);
                vec2 o_pos = vec2(o_pos_x, o_pos_y);
                vec2 o_vel = vec2(o_vel_x, o_vel_y);
                vec2 r_vec = o_pos - pos;
                float r = length(r_vec);
                if(r != 0) {
                    vec2 direction = normalize(r_vec);

                    // Electrostatic force calculation
                    float k = 1000.0;
                    float forceMag = k * charge * o_charge / (r * r);

                    if (forceMag <= 0) continue;

                    vec2 force = direction * forceMag;
                    vel -= force / mass;
                }

                // Collision
                float minDist = radius + o_radius;
                if (r == 0){
                    r_vec = normalize(vec2(random(), random()));
                    r = 0.001; // Prevent division by zero
                }

                if (r < minDist) {
                    float overlap = minDist - r;
                    vec2 direction = r_vec / r;
                    pos -= direction * overlap * 0.5;

                    // Simple bounce
                    vec2 relVel = o_vel - vel;
                    float velAlongNormal = dot(relVel, direction);
                    if (velAlongNormal <= 0) {
                        float restitution = 0.8;
                        float impulse = -(1.0 + restitution) * velAlongNormal / 2.0;
                        vec2 impulseVec = direction * impulse;
                        vel -= impulseVec / mass;
                    }
                }
            }
        }
    }
    set_atom(atom_index, pos.x, pos.y, vel.x, vel.y);
}