
const { chromium } = require('playwright');
(async () => {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage();
  await page.goto('file:///C:/GitHub1/TeachAndFight/tmp/pdfs/4_AI_%ED%99%9C%EC%9A%A9_%EA%B8%B0%EC%88%A0_%EB%AC%B8%EC%84%9C_%EA%B9%A8%EC%A7%90%EC%88%98%EC%A0%95.html', { waitUntil: 'load' });
  await page.pdf({
    path: 'C:\\GitHub1\\TeachAndFight\\output\\pdf\\4_AI_활용_기술_문서_상세본.pdf',
    format: 'A4',
    printBackground: true,
    preferCSSPageSize: true
  });
  await browser.close();
})().catch(err => {
  console.error(err);
  process.exit(1);
});
