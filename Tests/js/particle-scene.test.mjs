// The particle background's drawing and animation (Client/wwwroot/js/particle-scene.js), which runs in a worker or,
// where the browser can't, on the page. Run with: node --test "Tests/js/*.test.mjs"
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { createScene } from '../../Client/wwwroot/js/particle-scene.js';

// A canvas whose 2D context records the dots of the latest drawing, and a frame clock the test advances by hand.
function setUp({ reduceMotion = false } = {}) {
    const dots = [];
    const context = {
        clearRect() { dots.length = 0; },
        setTransform(...matrix) { this.matrix = matrix; },
        arc(x, y) { dots.push([x, y]); },
        beginPath() {}, moveTo() {}, lineTo() {}, stroke() {}, fill() {}
    };
    const canvas = { width: 0, height: 0, getContext: () => context };
    const frames = [];
    let seed = 1;
    const scene = createScene(canvas, {
        reduceMotion,
        requestFrame: callback => frames.push(callback),
        random: () => (seed = (seed * 16807) % 2147483647) / 2147483647
    });
    const frame = time => frames.shift()?.(time);
    return { scene, canvas, context, dots, frames, frame };
}

test('a resize sizes the canvas for the pixel ratio and draws 40 dots per 1000 x 800', () => {
    const { scene, canvas, context, dots } = setUp();

    scene.handle({ type: 'resize', width: 1000, height: 800, ratio: 2 });

    assert.deepEqual([canvas.width, canvas.height], [2000, 1600]);
    assert.deepEqual(context.matrix, [2, 0, 0, 2, 0, 0]);
    assert.equal(dots.length, 40);
});

test('with motion, every frame moves the dots', () => {
    const { scene, dots, frame } = setUp();
    scene.handle({ type: 'resize', width: 1000, height: 800, ratio: 1 });

    frame(0);
    const before = dots.map(dot => [...dot]);
    frame(100);

    assert.notDeepEqual(dots, before);
});

test('frames closer together than 60 a second are skipped', () => {
    const { scene, dots, frame } = setUp();
    scene.handle({ type: 'resize', width: 1000, height: 800, ratio: 1 });

    frame(0);
    const before = dots.map(dot => [...dot]);
    frame(5);

    assert.deepEqual(dots, before);
});

test('with reduced motion the dots are drawn and stand still', () => {
    const { scene, dots, frames } = setUp({ reduceMotion: true });

    scene.handle({ type: 'resize', width: 1000, height: 800, ratio: 1 });

    assert.equal(dots.length, 40);
    assert.equal(frames.length, 0);
});

test('a click adds four dots, up to twice the usual number', () => {
    const { scene, dots, frame } = setUp();
    scene.handle({ type: 'resize', width: 1000, height: 800, ratio: 1 });

    scene.handle({ type: 'click', x: 500, y: 400 });
    frame(0);
    assert.equal(dots.length, 44);

    for (let i = 0; i < 20; i++) scene.handle({ type: 'click', x: 500, y: 400 });
    frame(100);
    assert.equal(dots.length, 80);
});

test('the pointer pushes nearby dots away until it leaves', () => {
    const { scene, dots, frame } = setUp();
    scene.handle({ type: 'resize', width: 1000, height: 800, ratio: 1 });
    frame(0);

    scene.handle({ type: 'pointermove', x: dots[0][0], y: dots[0][1] + 10 });
    const closest = () => Math.min(...dots.map(([x, y]) => Math.hypot(x - pointerX, y - pointerY)));
    const [pointerX, pointerY] = [dots[0][0], dots[0][1] + 10];
    frame(100);
    const pushed = closest();
    scene.handle({ type: 'pointerleave' });

    assert.ok(pushed > 10, `the closest dot should be pushed past 10 px, is ${pushed}`);
});
