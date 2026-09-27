const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const ts = require(process.env.AVENTUS_TYPESCRIPT_PATH || 'typescript');

const files = ['ISocket', 'Socket', 'Connection', 'EndPoint', 'SSEEvent', 'type'];
const source = files.map(name => fs.readFileSync(path.join(__dirname, '../src/SSE', name + '.lib.avt'), 'utf8')
    .replace(/^import .*;\r?\n/gm, '')).join('\n');

function setup() {
    class Callback {
        callbacks = new Set();
        add(cb) { this.callbacks.add(cb); }
        remove(cb) { this.callbacks.delete(cb); }
        trigger(...args) { for (const cb of [...this.callbacks]) cb(...args); }
    }
    class ActionGuard {
        pending;
        run(keys, cb) {
            if (!this.pending) this.pending = cb().finally(() => this.pending = undefined);
            return this.pending;
        }
    }
    class FakeSource {
        static OPEN = 1;
        static CLOSED = 2;
        static instances = [];
        readyState = 0;
        constructor(url, options) { this.url = url; this.options = options; FakeSource.instances.push(this); }
        open() { this.readyState = 1; this.onopen?.({}); }
        error() { this.readyState = 0; this.onerror?.({}); }
        close() { this.readyState = 2; }
        message(channel, data) { this.onmessage?.({ data: JSON.stringify({ channel, data: JSON.stringify(data) }) }); }
    }
    class VoidWithError { }
    class ResultWithError extends VoidWithError { constructor(result) { super(); this.result = result; } }
    const context = vm.createContext({ console, URL, EventSource: FakeSource,
        window: { EventSource: FakeSource, location: new URL('https://example.com/app') },
        Aventus: { Callback, ActionGuard, VoidWithError, ResultWithError,
            Instance: { get: type => new type() },
            Converter: { transform: data => data.kind === 'result' ? new ResultWithError(data.result) : data },
            Uri: {
                prepare: channel => ({ regex: channel, params: {} }),
                getParams: (info, channel) => info.regex === channel ? {} : null
            }
        }
    });
    const compiled = ts.transpileModule(source, { compilerOptions: { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.None } });
    vm.runInContext(compiled.outputText, context);
    vm.runInContext(`class TestEndpoint extends SSE.EndPoint {
        configure(options) { options.autoStart = false; return super.configure(options); }
    }
    class TestEvent extends SSE.SSEEvent { path() { return '/changed'; } }
    this.TestEndpoint = TestEndpoint; this.TestEvent = TestEvent;`, context);
    return { context, FakeSource };
}

test('shared connections open once and close only after the last owner', async () => {
    const { context, FakeSource } = setup();
    const a = new context.TestEndpoint(), b = new context.TestEndpoint();
    const opened = [a.open(), b.open(), a.open()];
    assert.equal(FakeSource.instances.length, 1);
    const source = FakeSource.instances[0];
    assert.equal(source.url, 'https://example.com/sse');
    source.open();
    assert.deepEqual(await Promise.all(opened), [true, true, true]);
    a.close();
    assert.equal(source.readyState, 1);
    b.close();
    assert.equal(source.readyState, 2);
});

test('events listen, deduplicate, unwrap converted results and unsubscribe', async () => {
    const { context, FakeSource } = setup();
    const endpoint = new context.TestEndpoint();
    const event = new context.TestEvent(endpoint);
    const received = [];
    event.onTrigger.add(value => received.push(value));
    event.listen(); event.listen();
    const opening = endpoint.open();
    const source = FakeSource.instances[0]; source.open(); await opening;
    source.message('/changed', { kind: 'result', result: 42 });
    source.message('/unrelated', 99);
    assert.deepEqual(received, [42]);
    event.stop(); source.message('/changed', 10);
    assert.deepEqual(received, [42]);
    endpoint.close();
});

test('native reconnection retains subscriptions and emits lifecycle callbacks', async () => {
    const { context, FakeSource } = setup();
    const endpoint = new context.TestEndpoint();
    let opens = 0, errors = 0, closes = 0, messages = 0;
    endpoint.onOpen.add(() => opens++); endpoint.onError.add(() => errors++);
    endpoint.onClose.add(() => closes++);
    endpoint.addRoute({ channel: '/changed', callback: () => messages++ });
    const first = endpoint.open(); const source = FakeSource.instances[0];
    source.error(); assert.equal(await first, false);
    const retry = endpoint.open(); source.open(); assert.equal(await retry, true);
    source.message('/changed', {});
    assert.equal(FakeSource.instances.length, 1);
    assert.deepEqual([opens, errors, closes, messages], [1, 1, 1, 1]);
    endpoint.close();
});

test('closing during opening settles the promise and permits a fresh connection', async () => {
    const { context, FakeSource } = setup();
    const endpoint = new context.TestEndpoint(); const opening = endpoint.open();
    endpoint.close(); assert.equal(await opening, false);
    const retry = endpoint.open(); FakeSource.instances[1].open();
    assert.equal(await retry, true); endpoint.close();
});

test('credential modes are isolated and permanently closed sources can be reopened', async () => {
    const { context, FakeSource } = setup();
    const first = context.SSE.Socket.getInstance('https://example.com/sse', {}, false);
    const second = context.SSE.Socket.getInstance('https://example.com/sse', {}, true);
    assert.notEqual(first, second);
    assert.equal(FakeSource.instances[1].options.withCredentials, true);
    const endpoint = new context.TestEndpoint();
    const opening = endpoint.open(); const source = FakeSource.instances[0]; source.open(); await opening;
    source.readyState = 2; source.onerror({});
    const retry = endpoint.open(); FakeSource.instances[2].open();
    assert.equal(await retry, true); endpoint.close();
});

test('malformed messages and throwing listeners do not stop other listeners', async () => {
    const { context, FakeSource } = setup();
    context.console = { log() {}, error() {} };
    const endpoint = new context.TestEndpoint(); let calls = 0;
    endpoint.addRoute({ channel: '/changed', callback: () => { throw Error('listener'); } });
    endpoint.addRoute({ channel: '/changed', callback: () => calls++ });
    const opening = endpoint.open(); const source = FakeSource.instances[0]; source.open(); await opening;
    source.onmessage({ data: 'invalid' }); source.message('/changed', {});
    assert.equal(calls, 1); endpoint.close();
});
