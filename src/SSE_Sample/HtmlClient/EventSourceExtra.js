class EventSourceExtra {
    constructor(url, options = {}) {
        this.INITIALIZING = -1;
        this.CONNECTING = 0;
        this.OPEN = 1;
        this.CLOSED = 2;

        this.url = url;
        this.headers = options.headers || {};
        this.payload = options.payload !== undefined ? options.payload : '';
        this.method = options.method || (this.payload ? 'POST' : 'GET');
        this.withCredentials = !!options.withCredentials;
        this.debug = options.debug || false;

        this.FIELD_SEPARATOR = ':';
        this.listeners = {};
        this.dataListeners = {};

        this.xhr = null;
        this.readyState = this.INITIALIZING;
        this.progress = 0;
        this.chunk = '';
        this.retry = null;
    }

    addEventListener(type, listener) {
        if (!this.listeners[type]) {
            this.listeners[type] = [];
        }
        if (!this.listeners[type].includes(listener)) {
            this.listeners[type].push(listener);
        }
    }

    removeEventListener(type, listener) {
        if (!this.listeners[type]) return;
        this.listeners[type] = this.listeners[type].filter(l => l !== listener);
        if (!this.listeners[type].length) delete this.listeners[type];
    }

    on(type, listener) {
        if (!this.dataListeners[type]) {
            this.dataListeners[type] = [];
        }
        if (!this.dataListeners[type].includes(listener)) {
            this.dataListeners[type].push(listener);
        }
    }

    off(type, listener) {
        if (!type || !this.dataListeners[type]) return;
        if (!listener) {
            delete this.dataListeners[type];
        } else {
            this.dataListeners[type] = this.dataListeners[type].filter(l => l !== listener);
            if (!this.dataListeners[type].length) delete this.dataListeners[type];
        }
    }

    _dispatchEvent(e) {
        if (!e) return;
        e.source = this;
        if (this.debug) console.log(e);
        if (typeof e.retry !== 'undefined') this.retry = e.retry;
        if (e.type === 'message' && e.data === '') return;

        const onHandler = 'on' + e.type;
        if (this[onHandler]) this[onHandler].call(this, e);
        if (e.type === 'message' && this.ondata) this.ondata.call(this, e);

        const emitEvent = (listeners, eventData) => {
            if (!listeners) return;
            listeners.forEach(listener => listener(eventData));
        };

        emitEvent(this.listeners[e.type], e);
        emitEvent(this.dataListeners[e.type], this._parseData(e.data));
        if (e.type === 'message') {
            emitEvent(this.listeners.data, e);
            emitEvent(this.dataListeners.data, this._parseData(e.data));
        }
    }

    _parseData(data) {
        try {
            return JSON.parse(data);
        } catch {
            return data;
        }
    }

    _setReadyState(state) {
        const event = new CustomEvent('readystatechange');
        event.readyState = state;
        this.readyState = state;
        this._dispatchEvent(event);
    }

    _onStreamFailure(e) {
        const event = new CustomEvent('error');
        event.cause = e.type;
        event.message = 'stream failure';
        event.retry = this.retry || false;
        this._dispatchEvent(event);

        if (this.retry) {
            setTimeout(() => this.stream(), this.retry);
        } else {
            this.close();
        }
    }

    _onStreamAbort() {
        const event = new CustomEvent('error');
        event.message = 'stream abort';
        event.retry = this.retry || false;
        this._dispatchEvent(event);
        this.close();
    }

    _onStreamProgress() {
        if (!this.xhr || this.xhr.status !== 200) {
            this._onStreamFailure({type: 'progress'});
            return;
        }

        if (this.readyState === this.CONNECTING) {
            this._dispatchEvent(new CustomEvent('open'));
            this._setReadyState(this.OPEN);
        }

        const data = this.xhr.responseText.substring(this.progress);
        this.progress += data.length;

        data.split(/(\r\n|\r|\n){2}/g).forEach(part => {
            if (part.trim().length === 0) {
                this._dispatchEvent(this._parseEventChunk(this.chunk.trim()));
                this.chunk = '';
            } else {
                this.chunk += part;
            }
        });
    }

    _onStreamLoaded() {
        this._onStreamProgress();
        this._dispatchEvent(this._parseEventChunk(this.chunk));
        this.chunk = '';
    }

    _parseEventChunk(chunk) {
        if (!chunk) return null;

        const e = {id: null, retry: undefined, data: '', event: 'message'};
        chunk.split(/\n|\r\n|\r/).forEach(line => {
            line = line.trimRight();
            const index = line.indexOf(this.FIELD_SEPARATOR);
            if (index <= 0 || !(line.substring(0, index) in e)) return;

            const field = line.substring(0, index);
            const value = line.substring(index + 1).trimLeft();
            e[field] = field === 'data' ? e[field] + value : value;
        });

        const event = new CustomEvent(e.event);
        event.data = e.data;
        event.id = e.id;
        event.retry = e.retry !== undefined ? (isNaN(e.retry) ? null : parseInt(e.retry)) : undefined;

        return event;
    }

    _checkStreamClosed() {
        if (this.xhr?.readyState === XMLHttpRequest.DONE) {
            this._setReadyState(this.CLOSED);
        }
    }

    fetch(url, options = {}) {
        if (this.readyState === this.CONNECTING || this.readyState === this.OPEN) {
            throw new Error('Connection in use, wait for close, or close before new fetch');
        }

        Object.assign(this, {
            headers: options.headers || {},
            withCredentials: options.withCredentials || false,
            method: options.method || 'GET',
            payload: options.payload || '',
            debug: options.debug || this.debug || false,
            url: url
        });

        this.stream();
    }

    stream() {
        if (this.readyState === this.CONNECTING || this.readyState === this.OPEN) {
            throw new Error('Connection in use, wait for close, or close before new stream');
        }

        this._setReadyState(this.CONNECTING);

        this.xhr = new XMLHttpRequest();
        this.xhr.addEventListener('progress', this._onStreamProgress.bind(this));
        this.xhr.addEventListener('load', this._onStreamLoaded.bind(this));
        this.xhr.addEventListener('readystatechange', this._checkStreamClosed.bind(this));
        this.xhr.addEventListener('error', this._onStreamFailure.bind(this));
        this.xhr.addEventListener('abort', this._onStreamAbort.bind(this));
        this.xhr.open(this.method, this.url);
        Object.entries(this.headers).forEach(([header, value]) => {
            this.xhr.setRequestHeader(header, value);
        });
        this.xhr.withCredentials = this.withCredentials;
        this.xhr.send(this.payload);
    }

    close() {
        if (this.readyState === this.CLOSED) return;
        this.xhr?.abort();
        this.xhr = null;
        this._setReadyState(this.CLOSED);
    }
}