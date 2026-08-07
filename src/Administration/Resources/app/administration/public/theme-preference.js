// Setzt das gespeicherte Farbschema, bevor das erste Bild steht. Ohne das rendert die
// Seite im Systemschema und springt sichtbar um, sobald die SPA bootet.
//
// Bewusst eine eigene Datei statt eines Inline-Skripts: Die Content-Security-Policy des
// Hosts erlaubt `script-src 'self'` ohne 'unsafe-inline'. Inline wurde das Skript vom
// Browser blockiert — und damit trat genau das Umspringen ein, das es verhindern sollte.
//
// Ein CSP-Hash wäre die Alternative gewesen. Er stünde aber in einem anderen Projekt als
// das Skript (die Policy liegt im Kern, das Skript in der Admin-Shell) und müsste bei jeder
// Änderung von Hand nachgezogen werden. Diese Kopplung hätte irgendwann jemand übersehen,
// und der Fehler wäre wieder still gewesen.
//
// Nicht als `defer`/`async` einbinden: Es muss vor dem ersten Paint laufen.
;(function () {
  try {
    var pref = localStorage.getItem('callora.admin.theme')
    if (pref === 'light' || pref === 'dark') {
      document.documentElement.setAttribute('data-theme', pref)
    }
  } catch (e) {
    /* Storage nicht verfügbar — dann entscheidet die Media Query */
  }
})()
