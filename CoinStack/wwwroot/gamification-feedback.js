window.gamificationFeedback = (function () {
    let _audioCtx = null;

    function getCtx() {
        if (!_audioCtx) {
            _audioCtx = new (window.AudioContext || window.webkitAudioContext)();
        }
        // Resume if suspended (browser autoplay policy)
        if (_audioCtx.state === 'suspended') {
            _audioCtx.resume();
        }
        return _audioCtx;
    }

    function playTone(ctx, frequency, startTime, duration, gain, type = 'sine') {
        const osc = ctx.createOscillator();
        const gainNode = ctx.createGain();
        osc.connect(gainNode);
        gainNode.connect(ctx.destination);
        osc.type = type;
        osc.frequency.setValueAtTime(frequency, startTime);
        gainNode.gain.setValueAtTime(0, startTime);
        gainNode.gain.linearRampToValueAtTime(gain, startTime + 0.01);
        gainNode.gain.exponentialRampToValueAtTime(0.001, startTime + duration);
        osc.start(startTime);
        osc.stop(startTime + duration + 0.05);
    }

    // Ascending two-note coin chime — warm & satisfying
    function playPositive() {
        try {
            const ctx = getCtx();
            const t = ctx.currentTime;
            playTone(ctx, 880, t, 0.18, 0.18, 'triangle');
            playTone(ctx, 1108, t + 0.12, 0.22, 0.15, 'triangle');
        } catch { /* ignore if audio not available */ }
    }

    // Gentle descending soft tone — non-shaming, just a nudge
    function playNegative() {
        try {
            const ctx = getCtx();
            const t = ctx.currentTime;
            playTone(ctx, 440, t, 0.25, 0.12, 'sine');
            playTone(ctx, 370, t + 0.18, 0.30, 0.10, 'sine');
        } catch { /* ignore */ }
    }

    // Triumphant ascending chord arpeggio for goal/celebration
    function playCelebration() {
        try {
            const ctx = getCtx();
            const t = ctx.currentTime;
            const notes = [523, 659, 784, 1047];
            notes.forEach((freq, i) => {
                playTone(ctx, freq, t + i * 0.09, 0.45, 0.14, 'triangle');
            });
            // Sustained chord underneath
            [523, 659, 784].forEach(freq => {
                playTone(ctx, freq, t + 0.36, 0.6, 0.06, 'sine');
            });
        } catch { /* ignore */ }
    }

    function playSound(type) {
        switch (type) {
            case 'positive': playPositive(); break;
            case 'negative': playNegative(); break;
            case 'celebration': playCelebration(); break;
        }
    }

    // Spawns confetti particles into a target element
    function spawnConfetti(containerId) {
        const container = document.getElementById(containerId);
        if (!container) return;
        container.innerHTML = '';
        const colors = ['#f59e0b', '#10b981', '#3b82f6', '#8b5cf6', '#ef4444', '#ec4899', '#06b6d4'];
        for (let i = 0; i < 60; i++) {
            const el = document.createElement('div');
            const color = colors[i % colors.length];
            const left = Math.random() * 100;
            const delay = Math.random() * 0.8;
            const size = 6 + Math.random() * 8;
            const rotate = Math.random() * 360;
            el.style.cssText = `
                position: absolute;
                left: ${left}%;
                top: -10px;
                width: ${size}px;
                height: ${size * 0.5}px;
                background: ${color};
                border-radius: 2px;
                transform: rotate(${rotate}deg);
                animation: confettiFall ${1.8 + Math.random() * 1.2}s ${delay}s ease-in forwards;
                opacity: 0;
            `;
            container.appendChild(el);
        }
    }

    return { playSound, spawnConfetti };
})();
