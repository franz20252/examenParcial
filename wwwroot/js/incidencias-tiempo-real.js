// Actualización en tiempo real de /Operaciones/Incidencias con el WebSocket de PieSocket (API v3).
// URL: wss://{clusterId}.piesocket.com/v3/{canal}?api_key=... (mismo formato que usa el SDK oficial).
// Los eventos llegan como { event, data }, el formato de eventos del SDK de PieSocket.
(function () {
    'use strict';

    const EVENTO = 'IncidenciaActualizada';
    const RETARDO_INICIAL_MS = 1000;
    const RETARDO_MAXIMO_MS = 30000;
    const MS_ANTES_DE_RETIRAR = 1500;

    const indicador = document.getElementById('tiempo-real-estado');
    const listado = document.getElementById('incidencias-listado');
    if (!indicador || !listado || !('WebSocket' in window)) {
        return;
    }

    const url = 'wss://' + indicador.dataset.clusterId + '.piesocket.com/v3/'
        + encodeURIComponent(indicador.dataset.channel)
        + '?api_key=' + encodeURIComponent(indicador.dataset.apiKey)
        + '&notify_self=1';

    let socket = null;
    let temporizador = null;
    let intentos = 0;
    let requiereSincronizar = false;
    let detenido = false;
    let sincronizacionEnCurso = null;

    function mostrarEstado(texto, clase) {
        indicador.textContent = 'Tiempo real: ' + texto;
        indicador.className = 'small ' + clase;
        indicador.dataset.estado = texto;
    }

    function conectar() {
        temporizador = null;
        if (detenido || socket) {
            return;
        }

        mostrarEstado(requiereSincronizar ? 'reconectando…' : 'conectando…', 'text-warning');
        socket = new WebSocket(url);

        socket.onopen = function () {
            intentos = 0;
            mostrarEstado('conectado', 'text-success');
            // Tras una desconexión pueden haberse perdido eventos: se vuelve a pedir el estado actual.
            if (requiereSincronizar) {
                requiereSincronizar = false;
                sincronizar();
            }
        };
        socket.onmessage = function (e) {
            procesarMensaje(e.data);
        };
        // onerror siempre va seguido de onclose, que es quien programa la reconexión.
        socket.onclose = function () {
            socket = null;
            requiereSincronizar = true;
            programarReconexion();
        };
    }

    // Backoff exponencial con variación aleatoria (1 s, 2 s, 4 s… hasta 30 s).
    function programarReconexion() {
        if (detenido || temporizador) {
            return;
        }
        if (!navigator.onLine) {
            mostrarEstado('sin red; se reconectará al recuperarla', 'text-danger');
            return;
        }
        const base = Math.min(RETARDO_MAXIMO_MS, RETARDO_INICIAL_MS * Math.pow(2, intentos));
        const retardo = base / 2 + Math.random() * (base / 2);
        intentos++;
        mostrarEstado('desconectado; reintento en ' + Math.ceil(retardo / 1000) + ' s', 'text-danger');
        temporizador = setTimeout(conectar, retardo);
    }

    function procesarMensaje(texto) {
        let mensaje;
        try {
            mensaje = JSON.parse(texto);
            if (typeof mensaje === 'string') {
                mensaje = JSON.parse(mensaje);
            }
        } catch {
            return;
        }
        if (!mensaje) {
            return;
        }

        // PieSocket informa errores (API key o canal inválidos) con { error }: no se insiste.
        if (mensaje.error) {
            detenido = true;
            mostrarEstado('error de PieSocket (' + mensaje.error + ')', 'text-danger');
            if (socket) {
                socket.close();
            }
            return;
        }

        const datos = mensaje.event === EVENTO ? mensaje.data : (mensaje.type === EVENTO ? mensaje : null);
        if (datos && datos.id != null && datos.estado) {
            aplicarActualizacion(Number(datos.id), String(datos.estado));
        }
    }

    function aplicarActualizacion(id, estado) {
        const fila = listado.querySelector('tr[data-incidencia-id="' + id + '"]');
        if (!fila) {
            // Si una incidencia vuelve a estar abierta y no se muestra, se consulta el estado actual.
            if (estado === 'Abierta') {
                sincronizar();
            }
            return;
        }

        const celdaEstado = fila.querySelector('[data-campo="estado"]');
        if (celdaEstado) {
            celdaEstado.textContent = estado;
        }

        if (estado !== 'Abierta') {
            // Ya no pertenece al listado de abiertas: se marca y se retira sin recargar la página.
            fila.querySelectorAll('form').forEach(function (f) { f.remove(); });
            fila.classList.add('table-secondary');
            setTimeout(function () {
                fila.remove();
                if (!listado.querySelector('tr[data-incidencia-id]')) {
                    sincronizar();
                }
            }, MS_ANTES_DE_RETIRAR);
        }
    }

    // Vuelve a pedir la misma página (con la búsqueda actual) y sustituye solo el listado.
    function sincronizar() {
        if (sincronizacionEnCurso) {
            return sincronizacionEnCurso;
        }
        sincronizacionEnCurso = (async function () {
            try {
                const respuesta = await fetch(window.location.href, { credentials: 'same-origin', cache: 'no-store' });
                if (!respuesta.ok || new URL(respuesta.url).pathname !== window.location.pathname) {
                    mostrarEstado('no se pudo sincronizar (¿sesión caducada?)', 'text-danger');
                    return;
                }
                const html = new DOMParser().parseFromString(await respuesta.text(), 'text/html');
                const nuevo = html.getElementById('incidencias-listado');
                if (nuevo) {
                    listado.innerHTML = nuevo.innerHTML;
                    listado.dataset.sincronizado = new Date().toISOString();
                }
            } catch (e) {
                console.warn('No se pudo sincronizar el listado de incidencias', e);
            } finally {
                sincronizacionEnCurso = null;
            }
        })();
        return sincronizacionEnCurso;
    }

    // Si el navegador pierde la red, el socket puede quedar colgado: se cierra y se reconecta al volver.
    window.addEventListener('offline', function () {
        if (socket) {
            socket.close();
        }
    });
    window.addEventListener('online', function () {
        if (temporizador) {
            clearTimeout(temporizador);
            temporizador = null;
        }
        intentos = 0;
        conectar();
    });
    window.addEventListener('pagehide', function () {
        detenido = true;
        if (socket) {
            socket.close();
        }
    });
    // Página restaurada desde la caché del navegador (atrás/adelante): reconectar y sincronizar.
    window.addEventListener('pageshow', function (e) {
        if (e.persisted) {
            detenido = false;
            requiereSincronizar = true;
            intentos = 0;
            conectar();
        }
    });

    conectar();
})();
