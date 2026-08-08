// [auto-patch] Unity6 WebGL workerUrl 누락 크래시 우회용 no-op 워커.
self.onmessage=function(e){try{var d=e&&e.data?e.data:{};postMessage({id:d.id,decompressed:d.compressed});}catch(_){}};
postMessage({ready:true});
