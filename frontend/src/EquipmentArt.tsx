export default function EquipmentArt({ name, category }: { name: string; category: string }) {
  const item = name.toLowerCase()
  return <div className={`equipment-art ${category.toLowerCase()}`} aria-hidden="true">
    <svg viewBox="0 0 220 140" fill="none" xmlns="http://www.w3.org/2000/svg">
      <ellipse cx="110" cy="120" rx="54" ry="7" fill="currentColor" opacity=".09" />
      {item.includes('speaker') && !item.includes('stand') ? <>
        <path d="M80 22h56l8 7v79l-8 7H80V22Z" fill="#273d3a"/><path d="M80 22h54v93H80z" fill="#39544e"/>
        <rect x="86" y="28" width="42" height="80" rx="4" fill="#20332f"/><circle cx="107" cy="82" r="18" fill="#405a53"/>
        <circle cx="107" cy="82" r="12" stroke="#172b25" strokeWidth="4"/><circle cx="107" cy="82" r="5" fill="#1b3029"/>
        <rect x="94" y="36" width="26" height="14" rx="3" fill="#405a53"/><path d="M90 58h34" stroke="#405a53"/>
      </> : item.includes('microphone') ? <>
        <g transform="rotate(27 110 70)"><rect x="98" y="38" width="24" height="75" rx="9" fill="#304941"/>
        <rect x="93" y="17" width="34" height="37" rx="16" fill="#8b9b94"/><path d="M98 26h24M95 33h30M97 40h26" stroke="#556c61" strokeWidth="2"/>
        <rect x="105" y="61" width="10" height="16" rx="3" fill="#e5e9e1"/><path d="M108 112h4v8h-4" fill="#233a31"/></g>
      </> : item.includes('chair') ? <>
        <path d="m80 68-10 49m52-49 14 49M85 76l43 39m-9-38-43 39" stroke="#566b5a" strokeWidth="5" strokeLinecap="round"/>
        <rect x="75" y="27" width="54" height="40" rx="8" fill="#e9c58d"/><path d="M76 71h53l8 12H70l6-12Z" fill="#d7ae71"/>
        <path d="M80 65v8m45-8v8" stroke="#566b5a" strokeWidth="5"/>
      </> : item.includes('table') ? <>
        <path d="m67 71-9 46m41-46-11 46m38-46 14 46m18-46 13 46" stroke="#657767" strokeWidth="5" strokeLinecap="round"/>
        <path d="m50 60 97-12 28 18-99 15-26-21Z" fill="#d6ad77"/><path d="m50 60 26 21 99-15v7L76 88 50 67v-7Z" fill="#ba925e"/>
      </> : item.includes('gazebo') ? <>
        <path d="M61 58v62m98-62v62M73 55v55m74-55v55" stroke="#607469" strokeWidth="4"/>
        <path d="M48 60 110 24l64 36H48Z" fill="#526f59"/><path d="m110 24 25 36H83l27-36Z" fill="#779480"/>
        <path d="M48 60h126v10H48z" fill="#3f5944"/>
      </> : item.includes('festoon') ? <>
        <path d="M32 39q78 74 155 0" stroke="#6c785e" strokeWidth="3"/>
        {[55, 90, 126, 163].map((x, i) => <g key={x}><path d={`M${x} ${i === 0 || i === 3 ? 57 : 71}v10`} stroke="#6c785e" strokeWidth="3"/><ellipse cx={x} cy={i === 0 || i === 3 ? 79 : 93} rx="9" ry="12" fill="#efbc56"/><ellipse cx={x} cy={i === 0 || i === 3 ? 79 : 93} rx="14" ry="17" fill="#efbc56" opacity=".13"/></g>)}
      </> : category === 'Lighting' ? <>
        <path d="M110 78v35m0-9-30 16m30-16 30 16" stroke="#414e3a" strokeWidth="4" strokeLinecap="round"/>
        <path d="M85 47v37h50V47" stroke="#53674b" strokeWidth="5"/><circle cx="110" cy="47" r="27" fill="#40523d"/>
        <circle cx="110" cy="47" r="21" fill="#829961"/>{[[0,0],[-10,-10],[10,-10],[-10,10],[10,10],[0,-15],[0,15]].map(([x,y]) => <circle key={`${x}${y}`} cx={110+x} cy={47+y} r="4" fill="#e4d586"/>)}
      </> : item.includes('screen') ? <>
        <path d="M110 87v31m0-7-28 9m28-9 28 9" stroke="#53665b" strokeWidth="4"/>
        <rect x="55" y="25" width="110" height="66" rx="2" fill="#dee6dd"/><path d="M55 23h110M55 92h110" stroke="#53665b" strokeWidth="5" strokeLinecap="round"/>
        <path d="m87 69 20-21 13 13 13-10 16 18H87Z" fill="#b0c6b1"/>
      </> : item.includes('projector') ? <>
        <path d="m65 58 90-10 15 17-92 12-13-19Z" fill="#e5e6db"/><path d="M65 58v33l13 12 92-12V65L78 77 65 58Z" fill="#bdc7bb"/>
        <ellipse cx="139" cy="83" rx="15" ry="12" fill="#344b41"/><ellipse cx="139" cy="83" rx="9" ry="8" fill="#719388"/>
        <path d="M86 85h27m-27 6h27" stroke="#7c8e7d" strokeWidth="3"/>
      </> : item.includes('mixer') ? <>
        <path d="m65 43 90-7 10 65-95 12-5-70Z" fill="#3d5149"/>
        {[0,1,2,3,4,5].map(i => <g key={i}><path d={`M${79+i*13} 73v25`} stroke="#718c7b" strokeWidth="2"/><rect x={75+i*13} y={82+(i%3)*4} width="8" height="5" rx="1" fill="#d9decf"/><circle cx={77+i*13} cy="58" r="4" fill={i%2 ? '#d0a454' : '#89a091'}/></g>)}
      </> : <>
        <path d="M110 39v73m0-13-37 22m37-22 37 22" stroke="#566f5e" strokeWidth="5" strokeLinecap="round"/>
        <rect x="92" y="31" width="36" height="8" rx="4" fill="#3d5445"/>
        <path d="M105 72h10" stroke="#d9bc77" strokeWidth="4"/>
      </>}
    </svg>
  </div>
}
