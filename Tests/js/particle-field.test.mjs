// Tests for the particle background's movement rules. Run with: node --test Tests/js/
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { particleCount, step, linkOpacity, repulse, addParticles, rescale, MAX_STEP } from '../../Client/wwwroot/js/particle-field.js';

const dot = (x, y, vx = 0, vy = 0) => ({ x, y, vx, vy, radius: 1 });

test('40 dots fill 1000 x 800 px, and a larger screen gets proportionally more', () => {
    assert.equal(particleCount(1000, 800), 40);
    assert.equal(particleCount(2000, 800), 80);
    assert.equal(particleCount(0, 0), 0);
});

test('a dot moves by its speed for the time that passed', () => {
    const particles = [dot(10, 10, 600, -200)];
    step(particles, 0.05, 1000, 800);
    assert.deepEqual([particles[0].x, particles[0].y], [40, 0]);
});

test('a dot leaving one edge comes back at the opposite one', () => {
    const right = dot(995, 400, 600, 0);
    const top = dot(500, 5, 0, -600);
    step([right, top], 0.05, 1000, 800);
    assert.equal(right.x, 25);
    assert.equal(top.y, 775);
});

test('a long pause, such as a tab in the background, moves dots only one short step', () => {
    const particles = [dot(100, 100, 60, 0)];
    step(particles, 5, 1000, 800);
    assert.equal(particles[0].x, 100 + 60 * MAX_STEP);
});

test('links fade with distance and vanish at 150 px', () => {
    assert.equal(linkOpacity(0), 0.12);
    assert.equal(linkOpacity(75), 0.06);
    assert.equal(linkOpacity(150), 0);
    assert.equal(linkOpacity(200), 0);
});

test('the pointer pushes nearby dots away and leaves distant ones alone', () => {
    const near = dot(150, 100);
    const far = dot(300, 100);
    repulse([near, far], 100, 100, 0.1);
    assert.ok(near.x > 150, `near dot should move away, is at ${near.x}`);
    assert.equal(near.y, 100);
    assert.deepEqual([far.x, far.y], [300, 100]);
});

test('clicks add dots at the pointer, and the oldest go once the limit is reached', () => {
    const particles = [dot(1, 1), dot(2, 2), dot(3, 3)];
    addParticles(particles, 50, 60, 4, 5);
    assert.equal(particles.length, 5);
    assert.deepEqual(particles.map(p => [p.x, p.y]), [[3, 3], [50, 60], [50, 60], [50, 60], [50, 60]]);
});

test('dots keep their place relative to the screen when it changes size, such as a phone turned sideways', () => {
    const particles = [dot(195, 800), dot(390, 422)];
    rescale(particles, 390, 844, 844, 390);
    assert.deepEqual(particles.map(p => [p.x, p.y]), [[422, 800 * 390 / 844], [844, 195]]);
});

test('dots placed before the screen had a size stay where they are', () => {
    const particles = [dot(10, 20)];
    rescale(particles, 0, 0, 1000, 800);
    assert.deepEqual([particles[0].x, particles[0].y], [10, 20]);
});
