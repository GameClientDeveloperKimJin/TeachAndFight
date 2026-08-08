// [수동 패치용 스텁] Unity 6000.0.38f1 기본 WebGL 템플릿의 index.html 이 config.workerUrl 을
// 정의하지 않아 로더(Build.loader.js)의 cacheControl(m.workerUrl) 이 undefined.match 로
// 크래시하는 버그를 우회하기 위한 파일입니다.
//
// 이 빌드는 압축(Compression=Disabled)·스레드(webGLThreadsSupport=0) 모두 미사용이라
// 실제 디컴프레션/스레드 워커가 스폰되지 않습니다. 로더는 workerUrl 을 fetch 해서
// 블롭 URL 만 준비해 둘 뿐, 이 워커 코드를 실행하지 않으므로 아래 no-op 로 충분합니다.
self.onmessage = function (e) {
  try {
    var d = e && e.data ? e.data : {};
    postMessage({ id: d.id, decompressed: d.compressed });
  } catch (_) {}
};
postMessage({ ready: true });
