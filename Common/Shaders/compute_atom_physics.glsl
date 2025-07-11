#[compute]
#version 450

layout(local_size_x = 64, local_size_y = 1, local_size_z = 1) in;

layout(set = 0, binding = 0, std430) restrict buffer AtomBuffer {
    float atom_data[];
};

layout(set = 0, binding = 1, std430) restrict buffer ChunkInfoBuffer {
    int chunk_info[];
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

    // Find which chunk this atom belongs to
    int my_chunk = -1;
    int num_chunks = chunk_info[0];
    for (int c = 1; c <= num_chunks; ++c) {
        int start = chunk_info[c * 2 + 0];
        int count = chunk_info[c * 2 + 1];
        if (atom_index >= uint(start) && atom_index < uint(start + count)) {
            my_chunk = c;
            break;
        }
    }
    if (my_chunk == -1) return;

    // Repel and collision with atoms in this chunk and neighbors
    float2 pos = float2(pos_x, pos_y);
    float2 vel = float2(vel_x, vel_y);
    for (int dc = -1; dc <= 1; ++dc) {
        int neighbor_chunk = my_chunk + dc;
        if (neighbor_chunk < 0 || neighbor_chunk >= num_chunks) continue;
        int n_start = chunk_info[neighbor_chunk * 2 + 0];
        int n_count = chunk_info[neighbor_chunk * 2 + 1];
        for (int j = 0; j < n_count; ++j) {
            uint other_idx = uint(n_start + j);
            if (other_idx == atom_index) continue;
            float o_pos_x, o_pos_y, o_vel_x, o_vel_y, o_mass, o_charge, o_radius;
            get_atom(other_idx, o_pos_x, o_pos_y, o_vel_x, o_vel_y, o_mass, o_charge, o_radius);
            float2 o_pos = float2(o_pos_x, o_pos_y);
            float2 o_vel = float2(o_vel_x, o_vel_y);
            float2 delta = o_pos - pos;
            float dist = length(delta);
            float minDist = radius + o_radius;
            if (dist > 0.0) {
                // Repel force (Coulomb-like)
                float k = 1000.0;
                float forceMag = k * charge * o_charge / (dist * dist + 1e-4);
                if (forceMag > 0.0) {
                    float2 force = normalize(delta) * forceMag;
                    vel -= force / mass;
                }
                // Collision
                if (dist < minDist) {
                    float overlap = minDist - dist;
                    float2 direction = delta / dist;
                    pos -= direction * overlap * 0.5;
                    // Simple bounce
                    float2 relVel = o_vel - vel;
                    float velAlongNormal = dot(relVel, direction);
                    if (velAlongNormal < 0.0) {
                        float restitution = 0.8;
                        float impulse = -(1.0 + restitution) * velAlongNormal / 2.0;
                        float2 impulseVec = direction * impulse;
                        vel -= impulseVec;
                    }
                }
            }
        }
    }
    set_atom(atom_index, pos.x, pos.y, vel.x, vel.y);
}