// Run with Node.js; tests the webpage adapter without connecting to a website.
import fs from 'node:fs';
import vm from 'node:vm';
import assert from 'node:assert/strict';
const template = fs.readFileSync(new URL('./FillDraft.js', import.meta.url), 'utf8');
function check({ host='chat.deepseek.com', value='', count=1, hidden=false, disabled=false, text='hello' }={}) {
  let events=0;
  class TextArea {
    constructor(){ this._value=value; this.disabled=disabled; this.readOnly=false; }
    get value(){ return this._value; }
    set value(v){ this._value=v; }
    getAttribute(){ return '给 DeepSeek 发送消息'; }
    getClientRects(){ return hidden ? [] : [{}]; }
    dispatchEvent(){events++;}
    focus(){}
  }
  const elements=Array.from({length:count},()=>new TextArea());
  const result=vm.runInNewContext(template.replace('__DRAFT_JSON__',JSON.stringify(text)),{
    location:{protocol:'https:',hostname:host},
    document:{querySelectorAll:()=>elements},
    HTMLTextAreaElement:TextArea,Event:class {},
  });
  return {result,events,value:elements[0]?.value};
}
assert.deepEqual(check(),{result:'ok',events:1,value:'hello'});
assert.deepEqual(check({value:'unsent original'}),{result:'occupied',events:0,value:'unsent original'});
assert.equal(check({host:'evil.example'}).result,'wrong-origin');
assert.equal(check({count:0}).result,'missing');
assert.equal(check({count:2}).result,'missing');
assert.equal(check({hidden:true}).result,'missing');
assert.equal(check({disabled:true}).result,'missing');
const unusual='中文\n"\\\u2028<script>globalThis.pwned=true</script> ${anything}';
assert.equal(check({text:unusual}).value,unusual);
console.log('PASS: 8 draft-adapter cases (origin, existing text, missing/ambiguous/hidden/disabled input, special characters).');
