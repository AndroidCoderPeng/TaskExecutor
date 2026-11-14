  // ===== 数据 =====
  const DEVICE="Pixel 6";

  // 动作目录：与 C# 的 TaskChainAction 枚举一一对应；数组顺序=积木库展示顺序，执行顺序由用户编排
  const ACTIONS=[
    {id:"ScreenOn",   name:"亮屏",     desc:"唤醒屏幕并上滑解锁",      cmd:"input keyevent KEYCODE_WAKEUP"},
    {id:"OpenTarget", name:"打开应用", desc:"启动所选目标 APP",        cmd:"am start -n"},
    {id:"ScreenCapture",name:"截屏",   desc:"截图并保存到本地目录",    cmd:"screencap -p"},
    {id:"SendMessage",name:"发送消息", desc:"向当前焦点输入框发送文本", cmd:"input text"},
    {id:"KillTarget", name:"关闭应用", desc:"强制结束目标 APP",        cmd:"am force-stop"},
    {id:"ScreenOff",  name:"息屏",     desc:"发送按键指令关闭屏幕",    cmd:"input keyevent KEYCODE_SLEEP"},
  ];
  const actMeta=id=>ACTIONS.find(a=>a.id===id)||{name:id,desc:"",cmd:""};
  // 积木分类色：任务表 chip 与积木同色，保持"同一动作=同一颜色"的视觉关联
  const ACT_KIND={
    ScreenOn:{cat:"屏幕",color:"#007AFF"},
    OpenTarget:{cat:"应用",color:"#7B5CFA"},
    ScreenCapture:{cat:"采集",color:"#12A594"},
    SendMessage:{cat:"消息",color:"#E8930C"},
    KillTarget:{cat:"应用",color:"#7B5CFA"},
    ScreenOff:{cat:"屏幕",color:"#007AFF"},
  };
  const actColor=id=>(ACT_KIND[id]||{}).color||"#007AFF";

  let tasks=[
    {name:"工作日打卡",app:"钉钉",pkg:"com.alibaba.android.rimet",time:"07:30 ±5min",exec:"07:32",notify:"邮件",on:true,
      acts:["ScreenOn","OpenTarget","ScreenCapture"]},
    {name:"会议提醒",app:"飞书",pkg:"com.ss.android.lark",time:"08:50 ±3min",exec:"08:51",notify:"企业微信",on:true,
      acts:["ScreenOn","OpenTarget"]},
    {name:"日报截图",app:"企业微信",pkg:"com.tencent.wework",time:"18:00",exec:"18:00",notify:"邮件",on:true,
      acts:["ScreenOn","OpenTarget","ScreenCapture","KillTarget"]},
    {name:"周末巡检",app:"企业微信",pkg:"com.tencent.wework",time:"09:00 ±10min",exec:"—",notify:"",on:false,
      acts:["ScreenOn","SendMessage","ScreenOff"],payload:"巡检完成，设备状态正常"},
  ];
  let apps=[
    {name:"钉钉",pkg:"com.alibaba.android.rimet",act:"com.alibaba.android.rimet.biz.LaunchHomeActivity"},
    {name:"飞书",pkg:"com.ss.android.lark",act:"com.ss.android.lark.main.app.MainActivity"},
    {name:"企业微信",pkg:"com.tencent.wework",act:"com.tencent.wework.launch.WwMainActivity"},
  ];
  // 日志格式与 SchedulerService 一致：任务级一条汇总 + 心跳翻转，无动作级日志
  let logs=[
    {t:"11-12 07:32:41",lv:"ok",l:"成功",m:"任务「工作日打卡」执行成功：已启动 钉钉"},
    {t:"11-12 08:51:26",lv:"ok",l:"成功",m:"任务「会议提醒」执行成功：已启动 飞书"},
    {t:"11-12 11:58:44",lv:"err",l:"失败",m:"ADB 心跳：设备连接已断开（192.168.1.23:5555）"},
    {t:"11-12 11:59:16",lv:"ok",l:"成功",m:"ADB 心跳：设备已恢复连接（192.168.1.23:5555）"},
    {t:"11-12 18:00:05",lv:"err",l:"失败",m:"任务「日报截图」执行失败：截屏失败（退出码 1）"},
  ];
  let logsMore=[
    {t:"11-11 07:33:11",lv:"ok",l:"成功",m:"任务「工作日打卡」执行成功：已启动 钉钉"},
    {t:"11-11 09:00:14",lv:"err",l:"失败",m:"任务「周末巡检」执行失败：设备未授权 USB 调试，已跳过本轮"},
  ];
  let filter="all",editIndex=-1,logExpanded=false;
  let randCfg={min:0,max:5}; // 全局随机偏移范围，与「调度 → 随机时间范围」设置联动

  const $=id=>document.getElementById(id);
  const VIEW_META={
    tasks:{title:"任务管理",desc:""},
    records:{title:"任务记录",desc:"历史执行记录 · 本地保留 7 天"},
    wifi:{title:"无线连接",desc:"Wi-Fi ADB · 仅支持无线"},
    sync:{title:"数据导入导出",desc:"配置备份与恢复"},
    apps:{title:"目标 APP",desc:"包名映射管理"},
    mail:{title:"邮箱配置",desc:"QQ 邮箱 · SMTP"},
    wecom:{title:"企业微信配置",desc:"群机器人 Webhook"},
    wait:{title:"任务等待时间",desc:"动作链步骤间隔"},
    reset:{title:"任务重置时间",desc:"每日任务链重置点"},
    random:{title:"随机时间范围",desc:"执行时间随机偏移"},
    switches:{title:"调度开关",desc:"节假日 / 重置"},
    cmds:{title:"指令测试",desc:"ADB 指令调试"},
    faq:{title:"常见问题",desc:"HTML 帮助页"},
    about:{title:"关于",desc:"版本与项目信息"},
  };

  // ===== 视图切换 =====
  let curView="wifi";
  function switchView(v){
    if(v!=="wifi"&&!wifiConnected){toast("请先在「无线连接」中连接设备");return;}
    curView=v;
    document.querySelectorAll(".nav-item").forEach(x=>x.classList.toggle("active",x.dataset.view===v));
    document.querySelectorAll(".view").forEach(x=>x.classList.toggle("on",x.dataset.view===v));
    $("pageTitle").textContent=VIEW_META[v].title;
    $("pageDesc").textContent=VIEW_META[v].desc;
    $("btnNew").style.display=v==="tasks"?"":"none";
    document.querySelector(".content").scrollTop=0;
  }
  document.querySelectorAll(".nav-item").forEach(n=>n.addEventListener("click",()=>switchView(n.dataset.view)));

  // ===== Toast =====
  let toastTimer;
  function toast(msg){
    $("toastMsg").textContent=msg;
    $("toast").classList.add("show");
    clearTimeout(toastTimer);
    toastTimer=setTimeout(()=>$("toast").classList.remove("show"),2200);
  }

  // ===== 运行日志 =====
  function nowStr(){
    const d=new Date(),p=n=>String(n).padStart(2,"0");
    return p(d.getMonth()+1)+"-"+p(d.getDate())+" "+p(d.getHours())+":"+p(d.getMinutes())+":"+p(d.getSeconds());
  }
  function addLog(lv,l,m){
    logs.push({t:nowStr(),lv,l,m});
    renderLogs();
    const list=$("logList");list.scrollTop=list.scrollHeight;
  }
  function renderLogs(){
    const list=logExpanded?logs.concat(logsMore):logs;
    $("logList").innerHTML=list.map(l=>`
      <div class="log-row"><span class="t">${l.t}</span><span class="lv ${l.lv}">${l.l}</span><span class="m">${l.m}</span></div>`).join("");
    $("logToggle").textContent=logExpanded?"收起":"查看全部";
  }
  $("logToggle").onclick=()=>{logExpanded=!logExpanded;renderLogs();};

  // ===== 任务表格 =====
  function chipsHtml(k){
    const acts=k.acts||[];
    // XAML ItemsControl 全量展示动作，不做截断；空动作链显示 —
    if(!acts.length)return '<span class="pkg">—</span>';
    return `<div class="chips">${acts.map(id=>{
      const a=actMeta(id);
      const tip=id==="SendMessage"&&k.payload?`${a.name}：${k.payload}`:`${a.name} · ${a.desc}`;
      const c=actColor(id);
      return `<span class="chip" style="color:${c};background:${c}1f" title="${tip}">${a.name}</span>`;
    }).join("")}</div>`;
  }
  // 通知方式：描边标签（XAML Border 1px Color + 前景 Color，无背景）
  const NOTIFY_COLOR={"邮件":"#007AFF","企业微信":"#12A594"};
  function notifyHtml(k){
    const n=(k.notify||"").trim();
    if(!n)return '<span class="pkg">—</span>';
    return n.split(/[、,，]/).filter(Boolean).map(x=>{
      const c=NOTIFY_COLOR[x.trim()]||"#007AFF";
      return `<span class="chip outline" style="color:${c};border-color:${c}">${x.trim()}</span>`;
    }).join("");
  }
  // 计划时间：两行文本 = 基准时间 + 实际随机执行窗口；k.time 形如 "07:30 ±5min" / "07:30 +0~5min" / "07:30 +2min" / "18:00"
  function planHtml(time){
    const m=/^(\d{1,2}:\d{2})(?:\s+(.+))?$/.exec((time||"").trim());
    if(!m)return `<span class="plan-time">${time||"-"}</span>`;
    const base=m[1],off=(m[2]||"").trim();
    const hm=v=>String(Math.floor(v/60)%24).padStart(2,"0")+":"+String(Math.round(v%60)).padStart(2,"0");
    let lo=null,hi=null,p;
    if((p=/^±(\d+)min$/.exec(off))){lo=0;hi=+p[1];}
    else if((p=/^\+(\d+)~(\d+)min$/.exec(off))){lo=+p[1];hi=+p[2];}
    else if((p=/^\+(\d+)min$/.exec(off))){lo=hi=+p[1];}
    const q=/^(\d{1,2}):(\d{2})$/.exec(base),mins=(+q[1])*60+ +q[2];
    if(hi==null||hi<=0)
      return `<span class="plan-txt" title="不做随机偏移，到点立即执行"><span class="plan-time">${base}</span></span>`;
    const tip=`实际执行 = ${base} 后 ${off.replace(/^\+/,"")} 内随机 → ${hm(mins+lo)} ~ ${hm(mins+hi)}`;
    return `<span class="plan-txt" title="${tip}"><span class="plan-time">${base}</span><span class="plan-win">${hm(mins+lo)} ~ ${hm(mins+hi)}</span></span>`;
  }
  function render(){
    const list=tasks.filter(k=>filter==="all"||String(k.on)===(filter==="on"?"true":"false"));
    $("taskBody").innerHTML=list.length?list.map((k)=>{
      const i=tasks.indexOf(k);
      return `<tr data-i="${i}" class="${k.on?"":"disabled"}">
        <td><div class="task-name">任务名称 - ${k.app}${k.running?'<span class="run-badge">执行中</span>':""}</div><div
            class="task-sub">${k.pkg}</div></td>
        <td>${planHtml(k.time)}</td>
        <td><span class="time-exec">${k.exec||"—"}</span></td>
        <td>${chipsHtml(k)}</td>
        <td>${notifyHtml(k)}</td>
        <td><button class="status-pill ${k.on?"on":"off"}" data-act="toggle"><span class="dot"></span>${k.on?"已启用":"已停用"}</button></td>
        <td><div class="ops"><a class="mute" data-act="toggle">${k.on?"暂停":"启用"}</a></div></td>
      </tr>`;}).join("")
      :`<tr class="empty-row"><td colspan="7">暂无任务，点击右上角「新建任务」添加</td></tr>`;
    $("countBadge").textContent=tasks.length+" 项";
  }
  $("taskBody").addEventListener("click",e=>{
    if(e.target.dataset.act!=="toggle")return;
    const i=+e.target.closest("tr").dataset.i;
    tasks[i].on=!tasks[i].on;
    toast("任务「"+tasks[i].name+"」已"+(tasks[i].on?"启用":"暂停"));
    render();
  });

  // 行右键菜单：运行 / 编辑 / 删除（对齐 DataGrid.ContextMenu）
  let ctxIndex=-1;
  $("taskBody").addEventListener("contextmenu",e=>{
    const tr=e.target.closest("tr[data-i]");
    if(!tr)return;
    e.preventDefault();
    ctxIndex=+tr.dataset.i;
    const menu=$("ctxMenu"),wr=document.querySelector(".win").getBoundingClientRect();
    menu.style.left=Math.min(e.clientX-wr.left,wr.width-160)+"px";
    menu.style.top=Math.min(e.clientY-wr.top,wr.height-140)+"px";
    menu.classList.add("show");
  });
  document.addEventListener("click",e=>{
    if(!e.target.closest("#ctxMenu"))$("ctxMenu").classList.remove("show");
  });
  $("ctxMenu").addEventListener("click",e=>{
    const item=e.target.closest(".ctx-item");
    if(!item)return;
    $("ctxMenu").classList.remove("show");
    const i=ctxIndex;
    if(item.dataset.ctx==="run")return runTask(i);
    if(item.dataset.ctx==="edit")return openDialog("edit",i);
    if(item.dataset.ctx==="del"){
      const name=tasks[i].name;
      tasks.splice(i,1);render();
      toast("任务「"+name+"」已删除");
    }
  });

  // 试运行：按动作链逐步执行，结束写一条任务级汇总日志（与调度器日志格式一致）
  let runningTask=false;
  function runTask(i){
    const k=tasks[i];
    if(runningTask){toast("已有任务正在执行，请稍候");return;}
    if(!k.acts||!k.acts.length){toast("该任务未配置动作链，无法运行");return;}
    if(k.acts.includes("SendMessage")&&!k.payload){toast("动作链含「发送消息」但未填写内容");return;}

    runningTask=true;
    k.running=true;render();
    const pill=$("chainPill");
    pill.hidden=false;$("chainPillText").textContent="任务「"+k.name+"」执行中";
    toast("正在试运行「"+k.name+"」");
    const steps=k.acts.slice();
    let n=0;
    const next=()=>{
      if(n>=steps.length){
        addLog("ok","成功","任务「"+k.name+"」试运行成功：完成 "+steps.length+" 个动作");
        toast("「"+k.name+"」试运行完成");
        pill.hidden=true;
        k.running=false;render();
        runningTask=false;return;
      }
      $("chainPillText").textContent=`任务「${k.name}」执行中 · ${n}/${steps.length}`;
      n++;setTimeout(next,900);
    };
    next();
  }

  // 筛选
  document.querySelectorAll(".filters span").forEach(f=>f.addEventListener("click",()=>{
    document.querySelectorAll(".filters span").forEach(x=>x.classList.remove("on"));
    f.classList.add("on");filter=f.dataset.f;render();
  }));

  // ===== 任务弹窗 =====
  const overlay=$("overlay");
  let offsetEnabled=true;
  const fmtHM=m=>{m=((m%1440)+1440)%1440;return String(Math.floor(m/60)).padStart(2,"0")+":"+String(m%60).padStart(2,"0");};
  function offsetSuffix(){
    if(!offsetEnabled)return "";
    const lo=randCfg.min,hi=randCfg.max;
    return lo===hi?` +${lo}min`:lo===0?` ±${hi}min`:` +${lo}~${hi}min`;
  }
  function parseOffset(str){
    return /±(\d+)min/.test(str)||/\+(\d+)~(\d+)min/.test(str);
  }
  function updateOffsetHint(){
    const el=$("offsetHint"),v=$("fTime").textContent.trim();
    if(!offsetEnabled){el.textContent="不做随机偏移，到点立即执行";return;}
    const lo=randCfg.min,hi=randCfg.max;
    if(lo>hi){el.textContent="随机范围设置有误：下限不能大于上限";return;}
    const mm=/^(\d{1,2}):(\d{1,2})$/.exec(v);
    if(!mm){el.textContent=`设定时间 + ${lo}~${hi} 分钟随机`;return;}
    el.textContent=`实际 ${fmtHM(+mm[1]*60+ +mm[2]+lo)} ~ ${fmtHM(+mm[1]*60+ +mm[2]+hi)} 随机执行`;
  }
  function refreshOffsetUI(){
    $("offsetSwitch").classList.toggle("on",offsetEnabled);
    updateOffsetHint();
  }
  function fillAppOptions(selectedPkg){
    $("fApp").innerHTML='<option value="">请选择目标 APP</option>'
      +apps.map(a=>`<option value="${a.pkg}" ${a.pkg===selectedPkg?"selected":""}>${a.name} · ${a.pkg}</option>`).join("");
  }
  // ===== 动作积木（积木式编排）=====
  let draftActs=[];     // 序列顺序即执行顺序，由用户拖拽决定
  let draftPayload="";  // 「发送消息」积木的内联参数
  let dragSrc=null;     // {id, from:"pal"|"chain"}
  let saveTried=false;  // 只有点击保存后才把空序列标红

  function palHtml(a){
    const added=draftActs.includes(a.id),c=actColor(a.id);
    return `<div class="pal-chip${added?" added":""}" data-id="${a.id}"
      draggable="${!added}" title="${a.cmd}" style="--c:${c};--c-bg:${c}1c">
      <span>${a.name}</span><span class="plus">${added?"✓":"+"}</span>
    </div>`;
  }

  function blkHtml(id,idx){
    const a=actMeta(id),c=actColor(id);
    let param;
    if(id==="SendMessage"){
      param=`<input class="bin" id="fPayload" draggable="false" placeholder="要发送的文本…" value="${draftPayload.replace(/"/g,"&quot;")}">`;
    }else if(id==="OpenTarget"||id==="KillTarget"){
      param=`<span class="bmeta">→ 所选目标 APP</span>`;
    }else{
      param=`<span class="bmeta">${a.cmd}</span>`;
    }
    return `<div class="blk" data-id="${id}" draggable="true" style="--c:${c};--c-bg:${c}1c">
      <span class="no">${idx+1}</span>
      <span class="bname">${a.name}</span>
      ${param}
      <span class="del" data-del="${id}" title="移除该动作">✕</span>
    </div>`;
  }

  function renderActs(){
    $("palList").innerHTML=ACTIONS.map(palHtml).join("");
    // 已编排的块 + 底部拖放提示；全部 6 块都在时不再显示
    const tail=draftActs.length>0&&draftActs.length<ACTIONS.length
      ?`<div class="chain-tail">拖到这里继续添加</div>`:"";
    $("chain").innerHTML=draftActs.length
      ? draftActs.map(blkHtml).join("")+tail
      : `<div class="chain-empty">把左边的动作积木拖到这里<br><small>也可以直接点击积木添加</small></div>`;
    $("chainCount").textContent=draftActs.length+" 步";
    $("actSummary").innerHTML=draftActs.length
      ? `从上到下依次执行，某一步失败会中止后续动作`
      : saveTried
        ? `<span class="empty">至少加入 1 个动作积木才能保存</span>`
        : `从积木库点选或拖入，编排从上到下执行的动作`;

    // 积木内联参数：只更新状态不重渲染，避免输入时丢焦点
    const input=$("fPayload");
    if(input){
      input.addEventListener("input",e=>{draftPayload=e.target.value;});
      input.addEventListener("mousedown",e=>e.stopPropagation());
      input.addEventListener("dragstart",e=>e.preventDefault());
    }
  }

  // —— 积木库：点击追加到序列末尾 ——
  $("palList").addEventListener("click",e=>{
    const el=e.target.closest(".pal-chip");
    if(!el||el.classList.contains("added"))return;
    draftActs.push(el.dataset.id);
    renderActs();
  });

  $("palList").addEventListener("dragstart",e=>{
    const el=e.target.closest(".pal-chip");
    if(!el||el.classList.contains("added")){e.preventDefault();return;}
    dragSrc={id:el.dataset.id,from:"pal"};
    e.dataTransfer.effectAllowed="copy";
    e.dataTransfer.setData("text/plain",el.dataset.id);
  });

  $("palList").addEventListener("dragend",()=>{dragSrc=null;});

  // —— 执行序列：删除 / 拖拽排序 / 插入 ——
  function clearDropMarks(){
    document.querySelectorAll(".blk.drop-before,.blk.dragging").forEach(x=>x.classList.remove("drop-before","dragging"));
  }
  function hoverBlock(e){
    const el=document.elementFromPoint(e.clientX,e.clientY);
    return el?el.closest(".blk"):null;
  }

  $("chain").addEventListener("click",e=>{
    const del=e.target.closest("[data-del]");
    if(!del)return;
    draftActs=draftActs.filter(id=>id!==del.dataset.del);
    renderActs();
  });

  $("chain").addEventListener("dragstart",e=>{
    const blk=e.target.closest(".blk");
    if(!blk)return;
    dragSrc={id:blk.dataset.id,from:"chain"};
    e.dataTransfer.effectAllowed="move";
    e.dataTransfer.setData("text/plain",blk.dataset.id);
    blk.classList.add("dragging");
  });

  $("chain").addEventListener("dragover",e=>{
    e.preventDefault();
    document.querySelectorAll(".blk.drop-before").forEach(x=>x.classList.remove("drop-before"));
    const blk=hoverBlock(e);
    if(blk&&(!dragSrc||blk.dataset.id!==dragSrc.id))blk.classList.add("drop-before");
    $("chain").classList.add("drop");
  });

  $("chain").addEventListener("dragleave",e=>{
    // 只在真正离开画布时清理，避免子元素间移动导致指示线闪烁
    if(e.relatedTarget&&$("chain").contains(e.relatedTarget))return;
    $("chain").classList.remove("drop");
    clearDropMarks();
  });

  $("chain").addEventListener("drop",e=>{
    e.preventDefault();
    const blk=hoverBlock(e);
    const targetId=blk?blk.dataset.id:null;
    const src=dragSrc;
    dragSrc=null;
    $("chain").classList.remove("drop");
    clearDropMarks();
    if(!src)return;
    if(src.from==="chain"&&targetId===src.id)return;

    if(src.from==="chain")draftActs=draftActs.filter(x=>x!==src.id);
    const at=targetId?draftActs.indexOf(targetId):-1;
    draftActs.splice(at<0?draftActs.length:at,0,src.id);
    renderActs();
  });

  $("chain").addEventListener("dragend",()=>{
    dragSrc=null;
    $("chain").classList.remove("drop");
    clearDropMarks();
  });

  $("chainClear").addEventListener("click",()=>{
    if(!draftActs.length)return;
    draftActs=[];
    renderActs();
  });

  function openDialog(mode,i){
    editIndex=(mode==="edit")?i:-1;
    $("dlgTitle").textContent=mode==="edit"?"编辑任务":"新建任务";
    $("fName").textContent=mode==="edit"?tasks[i].name:"";
    const t=mode==="edit"?tasks[i].time:"";
    $("fTime").textContent=t.split(" ")[0];
    offsetEnabled=mode==="edit"?parseOffset(t):true;
    fillAppOptions(mode==="edit"?(tasks[i].pkg||""):"");

    // 新建默认预置「亮屏 → 打开应用」，与旧任务的迁移结果保持一致
    draftActs=mode==="edit"?(tasks[i].acts||[]).slice():["ScreenOn","OpenTarget"];
    draftPayload=mode==="edit"?(tasks[i].payload||""):"";
    saveTried=false;
    // 通知方式单选回填
    document.querySelectorAll(".tag-tg").forEach(c=>c.classList.toggle("on",mode==="edit"&&c.dataset.tag===tasks[i].notify));
    renderActs();
    refreshOffsetUI();
    overlay.classList.add("show");
    setTimeout(()=>{$("fName").focus();},50);
  }
  function closeDialog(){overlay.classList.remove("show");}
  $("btnNew").onclick=()=>openDialog("new");
  $("btnClose").onclick=closeDialog;
  $("btnCancel").onclick=closeDialog;
  overlay.addEventListener("click",e=>{if(e.target===overlay)closeDialog();});
  document.addEventListener("keydown",e=>{if(e.key==="Escape")closeDialog();});

  // 胶囊开关交互
  $("offsetSwitch").onclick=()=>{
    offsetEnabled=!offsetEnabled;
    refreshOffsetUI();
  };
  $("fTime").addEventListener("input",updateOffsetHint);

  $("btnSave").onclick=()=>{
    const name=$("fName").textContent.trim()||"未命名任务";
    const time=$("fTime").textContent.trim()||"08:00";
    const pkg=$("fApp").value;
    if(!pkg){toast("请选择目标 APP");return;}
    if(!draftActs.length){saveTried=true;renderActs();toast("请至少加入一个动作积木");return;}
    if(draftActs.includes("SendMessage")&&!draftPayload.trim()){toast("请填写「发送消息」积木的内容");return;}

    const app=apps.find(a=>a.pkg===pkg);
    const appName=app?app.name:"";
    const label=time+offsetSuffix();
    const acts=draftActs.slice();
    const payload=draftActs.includes("SendMessage")?draftPayload.trim():"";
    const notify=document.querySelector(".tag-tg.on")?.dataset.tag||"";

    if(editIndex>=0){
      Object.assign(tasks[editIndex],{name,app:appName,pkg,time:label,on:true,notify,acts,payload});
      toast("任务「"+name+"」已保存");
    }else{
      tasks.unshift({name,app:appName,pkg,time:label,exec:time,notify,on:true,acts,payload});
      toast("任务「"+name+"」已创建");
    }
    closeDialog();render();
  };

  // ===== 目标 APP 管理 =====
  let appEdit=-1;
  function renderApps(){
    $("appBody").innerHTML=apps.length?apps.map((a,i)=>`
      <tr data-i="${i}">
        <td><div class="task-name">${a.name}</div></td>
        <td><span class="pkg">${a.pkg}</span></td>
        <td><span class="pkg">${a.act||"—"}</span></td>
        <td style="text-align:right;"><div class="ops" style="justify-content:flex-end;"><a data-act="editApp">编辑</a><a
              class="danger" data-act="delApp" style="margin-left:10px;">删除</a></div></td>
      </tr>`).join("")
      :`<tr class="empty-row"><td colspan="4">暂无映射，在下方输入应用名称与包名后点击「添加目标 APP」</td></tr>`;
    $("appCount").textContent=apps.length+" 项";
  }
  function resetAppForm(){
    appEdit=-1;$("appName").value="";$("appPkg").value="";
    $("addApp").textContent="添加目标 APP";$("cancelApp").hidden=true;
  }
  $("appBody").addEventListener("click",e=>{
    const act=e.target.dataset.act;
    if(act!=="delApp"&&act!=="editApp")return;
    const i=+e.target.closest("tr").dataset.i;
    if(act==="delApp"){
      const name=apps[i].name;
      if(appEdit===i)resetAppForm();
      apps.splice(i,1);renderApps();
      toast("已移除「"+name+"」包名映射");
    }else{
      appEdit=i;
      $("appName").value=apps[i].name;$("appPkg").value=apps[i].pkg;
      $("addApp").textContent="保存修改";$("cancelApp").hidden=false;
      $("appName").focus();
    }
  });
  $("addApp").onclick=()=>{
    const n=$("appName").value.trim(),p=$("appPkg").value.trim();
    if(!n||!p){toast("请填写应用名称与包名");return;}
    if(!/^[a-zA-Z][\w]*(\.[\w]+)+$/.test(p)){toast("包名格式不正确，如 com.example.app");return;}
    if(appEdit>=0){
      apps[appEdit].name=n;apps[appEdit].pkg=p;
      toast("已保存「"+n+"」映射修改");
    }else{
      apps.push({name:n,pkg:p,act:""});
      toast("已添加「"+n+"」映射");
      addLog("info","信息","新增包名映射："+n+" · "+p);
    }
    resetAppForm();renderApps();
  };
  $("cancelApp").onclick=resetAppForm;
  renderApps();

  // ===== 数据导入导出 =====
  document.querySelectorAll("[data-io]").forEach(b=>b.addEventListener("click",()=>{
    const io=b.dataset.io;
    if(io==="export-backup"){toast("备份已导出：TaskExecutor_备份_2026-11-13.json");addLog("ok","成功","完整备份已导出（任务/映射/通知/调度配置）");}
    if(io==="import-backup"){toast("已从备份恢复 4 条任务与 3 条包名映射（演示）");addLog("ok","成功","从备份文件恢复全部配置");}
  }));
  $("ioDrop").onclick=()=>{toast("已从备份恢复 4 条任务与 3 条包名映射（演示）");addLog("ok","成功","从备份文件恢复全部配置");};

  // ===== 保存 / 测试按钮 =====
  document.querySelectorAll("[data-save]").forEach(b=>b.addEventListener("click",()=>{
    if(b.dataset.save==="random"){
      const lo=Math.max(0,+$("randMin").value||0),hi=Math.max(0,+$("randMax").value||0);
      if(lo>hi){toast("偏移下限不能大于上限");return;}
      randCfg={min:lo,max:hi};
      if(overlay.classList.contains("show"))refreshOffsetUI();
      toast(`随机时间范围已保存（+${lo}~${hi} 分钟）`);
      addLog("ok","成功",`随机时间范围已更新为 +${lo}~${hi} 分钟，对所有「跟随全局」的任务生效`);
      return;
    }
    const m={mail:"邮箱配置",wecom:"企业微信配置",wait:"任务等待时间",reset:"任务重置时间"}[b.dataset.save];
    toast(m+"已保存");addLog("ok","成功",m+"已保存并生效");
  }));
  document.querySelectorAll("[data-test]").forEach(b=>b.addEventListener("click",()=>{
    if(b.dataset.test==="mail"){toast("测试邮件已发送，请查收");addLog("ok","成功","测试邮件已发送至 pengxh@qq.com（smtp.qq.com:465 SSL · 60s 超时）");}
    else{toast("测试消息已推送至企业微信群");addLog("ok","成功","企业微信测试消息已通过 Webhook 推送");}
  }));
  document.querySelectorAll("[data-switch]").forEach(s=>s.addEventListener("click",()=>{
    s.classList.toggle("on");
    toast("开关已"+(s.classList.contains("on")?"开启":"关闭"));
  }));
  // 通知方式：单选（对齐 WPF RadioButton 组），点击已选中的保持不变
  document.querySelectorAll("[data-tag]").forEach(t=>t.addEventListener("click",()=>{
    document.querySelectorAll("[data-tag]").forEach(x=>x.classList.remove("on"));
    t.classList.add("on");
  }));

  // 节假日同步
  $("syncHoliday").onclick=()=>{
    toast("节假日数据同步完成");
    $("holidaySyncTime").textContent=nowStr().replace("-","-")+" · 覆盖 2026 全年";
    addLog("ok","成功","节假日数据已从 chinese-days CDN 同步（覆盖 2026 全年）");
  };

  // ===== 自定义工作日（按星期循环） =====
  // 与 HolidayHelper 对齐：勾选的星期视为工作日；法定节假日仍按节假日数据跳过
  const WEEK_NAMES=["周一","周二","周三","周四","周五","周六","周日"];
  let workWeek=[true,true,true,true,true,false,false]; // 默认周一至周五
  function renderWeekPicker(){
    $("weekPicker").innerHTML=WEEK_NAMES.map((n,i)=>
      `<button type="button" class="tag-tg${workWeek[i]?" on":""}" data-w="${i}">${n}</button>`).join("");
  }
  $("weekPicker").addEventListener("click",e=>{
    const b=e.target.closest("[data-w]");
    if(!b)return;
    workWeek[+b.dataset.w]=!workWeek[+b.dataset.w];
    renderWeekPicker();
  });
  const openRest=()=>{renderWeekPicker();$("restOverlay").classList.add("show");};
  const closeRest=()=>$("restOverlay").classList.remove("show");
  $("btnCustomDay").onclick=openRest;
  $("restClose").onclick=closeRest;
  $("restCancel").onclick=closeRest;
  $("restOverlay").addEventListener("click",e=>{if(e.target===$("restOverlay"))closeRest();});
  document.addEventListener("keydown",e=>{if(e.key==="Escape")closeRest();});
  $("restSave").onclick=()=>{
    closeRest();
    const names=workWeek.map((on,i)=>on?WEEK_NAMES[i]:null).filter(Boolean).join("、")||"无";
    toast("自定义工作日已保存");
    addLog("ok","成功",`每周工作日已更新：${names}`);
  };

  // ===== 任务记录（待设计） =====

  // ===== 指令测试 =====
  document.querySelectorAll(".cmd-btn").forEach(b=>b.addEventListener("click",()=>{
    const names={wake:"亮屏",off:"息屏",cap:"截屏",open:"打开目标应用",close:"关闭目标应用",msg:"发送消息"};
    addLog("ok","成功","指令测试 · "+names[b.dataset.cmd]+"执行成功");
    toast("指令已下发");
  }));

  // ===== FAQ 折叠 =====
  document.querySelectorAll(".faq-q").forEach(q=>q.addEventListener("click",()=>{
    q.parentElement.classList.toggle("open");
  }));

  // ===== 关于 =====
  const HOME_URL="https://github.com/pengxianhong/Pengxh";
  $("openHome2").onclick=()=>{window.open(HOME_URL,"_blank");toast("已在浏览器打开项目主页");};
  $("copyHome").onclick=()=>{navigator.clipboard&&navigator.clipboard.writeText(HOME_URL);toast("链接已复制到剪贴板");};
  $("checkUpdate").onclick=()=>toast("当前已是最新版本 v2.3.0");

  // ===== 无线连接 =====
  const CONN_DEFAULT="192.168.1.23:5555";
  let wifiConnected=false;
  function refreshLock(){
    document.querySelectorAll(".nav-item").forEach(n=>{
      n.classList.toggle("locked",!wifiConnected&&n.dataset.view!=="wifi");
    });
  }
  function setWifi(on,addr){
    wifiConnected=on;
    const full=addr||CONN_DEFAULT;
    const dot=$("wifiDot"),st=$("wifiState");
    const as=$("adbStatus"),dv=$("adbDev");
    if(on){
      dot.classList.add("pulse");dot.classList.remove("off");
      st.textContent="Pixel 6 · "+full+" · 已连接";
      st.style.color="var(--text-1)";
      as.textContent="ADB 已连接";as.style.color="var(--green)";
      dv.textContent="Pixel 6 · "+full;
      $("svcBadge").style.display="";
      refreshLock();
      hbReset();
      if(curView==="wifi")switchView("tasks");
    }else{
      dot.classList.remove("pulse");dot.classList.add("off");
      st.textContent="未连接设备 · 请在下方完成配对与连接";
      st.style.color="var(--text-3)";
      as.textContent="ADB 未连接";as.style.color="var(--red)";
      dv.textContent="等待无线连接";
      $("svcBadge").style.display="none";
      refreshLock();
      hbStop();
      if(curView!=="wifi")switchView("wifi");
    }
  }
  $("btnConnect").onclick=()=>{
    if(wifiConnected){toast("已有设备连接，请先断开");return;}
    const ip=$("connAddr").value.trim();
    if(!/^\d{1,3}(\.\d{1,3}){3}$/.test(ip)){toast("请输入正确的设备 IP，如 192.168.1.23");return;}
    const addr=ip+":5555";
    const btn=$("btnConnect");
    btn.disabled=true;btn.textContent="连接中…";
    toast("连接请求已发送，请在设备上确认「允许 USB 调试」弹窗");
    setTimeout(()=>{
      btn.disabled=false;btn.textContent="连接设备";
      setWifi(true,addr);
      toast("无线连接成功");
      addLog("ok","成功","Wi-Fi ADB 已连接：Pixel 6 · "+addr);
    },1200);
  };
  $("btnDisconnect").onclick=()=>{
    if(!wifiConnected)return;
    setWifi(false);
    toast("已断开连接");
    addLog("info","信息","已断开无线连接（adb disconnect）");
  };
  $("btnRestartAdb").onclick=()=>{
    const btn=$("btnRestartAdb");
    if(btn.disabled)return;
    btn.disabled=true;btn.textContent="重启中…";
    setTimeout(()=>{
      btn.disabled=false;btn.textContent="重启 ADB 服务";
      toast("ADB 服务已重启");
      addLog("ok","成功","ADB 服务已重启（kill-server → start-server），无线连接自动恢复");
    },1000);
  };


  // ===== ADB 心跳 =====
  // 真实实现：30s 一次 adb devices 探测，掉线/恢复时写运行日志；界面靠秒数表达"心还在跳"
  // 原型演示加速：4s 一次成功探测
  const HB_DEMO_TICK = 4;
  let hbLast = Date.now();

  const hbElapsed = () => Math.floor((Date.now() - hbLast) / 1000);

  function hbReset() {
    hbLast = Date.now();
    renderHeartbeat();
  }

  function hbStop() {
    renderHeartbeat();
  }

  function renderHeartbeat() {
    const elapsed = hbElapsed();

    const t = $("adbHbText");
    t.className = "hb-text " + (wifiConnected ? "alive" : "");
    t.textContent = !wifiConnected ? "心跳 -" : `心跳 ${elapsed}s`;

    // 单一状态点：颜色表示连接状态，动画表示心跳节奏
    const dot = $("adbDot");
    dot.classList.toggle("beat", wifiConnected);
    dot.classList.toggle("off", !wifiConnected);
  }

  function restoreLinkUi() {
    $("adbStatus").textContent = "ADB 已连接";
    $("adbStatus").style.color = "var(--green)";
    $("wifiDot").classList.remove("off");
    $("wifiDot").classList.add("pulse");
    $("wifiState").textContent = "Pixel 6 · " + CONN_DEFAULT + " · 已连接";
    $("wifiState").style.color = "var(--text-1)";
  }

  // 每秒刷新"心跳 N 秒前"；每 4s 模拟一次成功探测
  setInterval(() => {
    if (wifiConnected && Date.now() - hbLast >= HB_DEMO_TICK * 1000) hbLast = Date.now();
    renderHeartbeat();
  }, 1000);

  // ===== 初始化 =====
  // 演示初始态：默认设备已连接，便于直接观察心跳跳动（不自动跳转页面）
  render();renderLogs();
  wifiConnected = true;
  restoreLinkUi();
  $("svcBadge").style.display = "";
  refreshLock();
  hbReset();

  // 演示：休息日提示药丸，点击循环切换三种状态（实际 WPF 中由调度器状态驱动）
  const restStates = [
    { cls: "badge orange", html: '<span class="dot sm"></span>休息日 · 任务暂停' },
    { cls: "badge gray", html: "今日休息日（跳过节假日未开启）" },
    { cls: "", html: "" } // 工作日：隐藏
  ];
  let restIdx = 0;
  $("restBadge").onclick = () => {
    restIdx = (restIdx + 1) % restStates.length;
    const s = restStates[restIdx];
    const b = $("restBadge");
    b.className = s.cls;
    b.innerHTML = s.html;
    b.hidden = s.cls === "";
    if (s.cls) b.title = s.cls.includes("orange")
      ? "今日为休息日，且计划设置已开启「跳过节假日」，到点任务不会触发"
      : "今日为休息日，任务正常执行";
  };
