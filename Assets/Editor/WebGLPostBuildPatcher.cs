// WebGL 빌드 후처리 자동 패치
//
// 목적: Unity 6000.0.38f1 기본 WebGL 템플릿이 생성하는 index.html 이 config.workerUrl 을
//       누락해, 로더(*.loader.js)의 cacheControl(m.workerUrl) 에서
//       "Cannot read properties of undefined (reading 'match')" 크래시가 발생한다.
//       빌드할 때마다 index.html 이 새로 생성되므로 매번 수동 패치가 필요했는데,
//       이 스크립트가 빌드 직후 자동으로 workerUrl 을 추가하고 no-op 워커 스텁을 생성한다.
//
// 이 파일은 Editor 전용이며, 실제 빌드가 끝난 뒤 자동 실행된다. 수동 개입 불필요.

#if UNITY_EDITOR
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

public static class WebGLPostBuildPatcher
{
    [PostProcessBuild(1000)]
    public static void OnPostProcessBuild(BuildTarget target, string pathToBuiltProject)
    {
        if (target != BuildTarget.WebGL) return;

        string indexPath = Path.Combine(pathToBuiltProject, "index.html");
        if (!File.Exists(indexPath))
        {
            Debug.LogWarning("[WebGLPatch] index.html 를 찾지 못함: " + indexPath);
            return;
        }

        string html = File.ReadAllText(indexPath);

        // 빌드 산출물 이름 추출: codeUrl: buildUrl + "/<NAME>.wasm"
        var m = Regex.Match(html, "codeUrl:\\s*buildUrl\\s*\\+\\s*\"/([^\"]+)\\.wasm\"");
        if (!m.Success)
        {
            Debug.LogWarning("[WebGLPatch] index.html 에서 codeUrl 을 찾지 못해 패치를 건너뜀");
            return;
        }
        string name = m.Groups[1].Value;

        // 1) config 에 workerUrl 이 없으면 codeUrl 줄 바로 뒤에 추가
        if (!html.Contains("workerUrl:"))
        {
            string codeLine = "codeUrl: buildUrl + \"/" + name + ".wasm\",";
            string patched = codeLine + "\n        workerUrl: buildUrl + \"/" + name +
                             ".worker.js\", // [auto-patch] Unity6 기본템플릿 workerUrl 누락 크래시 우회";
            html = html.Replace(codeLine, patched);
            File.WriteAllText(indexPath, html);
            Debug.Log("[WebGLPatch] index.html 에 workerUrl 추가 완료");
        }

        // 2) no-op 워커 스텁 생성 (압축/스레드 미사용 빌드라 실제 실행되지 않음)
        string workerPath = Path.Combine(pathToBuiltProject, "Build", name + ".worker.js");
        if (!File.Exists(workerPath))
        {
            File.WriteAllText(workerPath,
                "// [auto-patch] Unity6 WebGL workerUrl 누락 크래시 우회용 no-op 워커.\n" +
                "self.onmessage=function(e){try{var d=e&&e.data?e.data:{};postMessage({id:d.id,decompressed:d.compressed});}catch(_){}};\n" +
                "postMessage({ready:true});\n");
            Debug.Log("[WebGLPatch] 워커 스텁 생성: " + workerPath);
        }
    }
}
#endif
