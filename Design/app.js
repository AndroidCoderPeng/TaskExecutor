  // ===== 数据 =====
  const DEVICE="Pixel 6";

  // 动作目录：与 C# 的 TaskChainAction 枚举一一对应，数组下标即执行顺序
  const ACTIONS=[
    {id:"ScreenOn",   name:"亮屏",     desc:"唤醒屏幕并上滑解锁",      cmd:"input keyevent KEYCODE_WAKEUP"},
    {id:"OpenTarget", name:"打开应用", desc:"启动所选目标 APP",        cmd:"am start -n",  needApp:true},
    {id:"ScreenCapture",name:"截屏",   desc:"截图并保存到本地目录",    cmd:"screencap -p"},
    {id:"SendMessage",name:"发送消息", desc:"向当前焦点输入框发送文本", cmd:"input text",   needPayload:true},
    {id:"KillTarget", name:"关闭应用", desc:"强制结束目标 APP",        cmd:"am force-stop", needApp:true},
    {id:"ScreenOff",  name:"息屏",     desc:"发送按键指令关闭屏幕",    cmd:"input keyevent KEYCODE_SLEEP"},
  ];
  const actMeta=id=>ACTIONS.find(a=>a.id===id)||{name:id,desc:"",cmd:""};
  const actSort=(a,b)=>ACTIONS.findIndex(x=>x.id===a)-ACTIONS.findIndex(x=>x.id===b);

  let tasks=[
    {name:"工作日打卡",sub:"钉钉 · com.alibaba.android.rimet",time:"07:30 ±5min",notify:"邮件 + 企微",on:true,
      acts:["ScreenOn","OpenTarget","ScreenCapture"]},
    {name:"会议提醒",sub:"飞书 · com.ss.android.lark",time:"08:50 ±3min",notify:"企业微信",on:true,
      acts:["ScreenOn","OpenTarget"]},
    {name:"日报截图",sub:"企业微信 · com.tencent.wework",time:"18:00",notify:"邮件",on:true,
      acts:["ScreenOn","OpenTarget","ScreenCapture","KillTarget"]},
    {name:"周末巡检",sub:"企业微信 · com.tencent.wework",time:"09:00 ±10min",notify:"邮件",on:false,
      acts:["ScreenOn","SendMessage","ScreenOff"],payload:"巡检完成，设备状态正常"},
  ];
  let apps=[
    {name:"钉钉",pkg:"com.alibaba.android.rimet"},
    {name:"飞书",pkg:"com.ss.android.lark"},
    {name:"企业微信",pkg:"com.tencent.wework"},
  ];
  let logs=[
    {t:"10-24 07:35:02",lv:"ok",l:"成功",task:"工作日打卡",act:"亮屏",m:"唤醒成功"},
    {t:"10-24 07:35:05",lv:"ok",l:"成功",task:"工作日打卡",act:"打开应用",m:"已启动 钉钉 · com.alibaba.android.rimet"},
    {t:"10-24 07:35:09",lv:"ok",l:"成功",task:"工作日打卡",act:"截屏",m:"已保存 Screenshots/screencap_20261024_073509.png"},
    {t:"10-24 07:35:11",lv:"ok",l:"成功",task:"工作日打卡",m:"执行成功：完成 3/3 个动作，用时 9s"},
    {t:"10-24 08:52:10",lv:"ok",l:"成功",task:"会议提醒",act:"打开应用",m:"已启动 飞书 · com.ss.android.lark"},
    {t:"10-24 11:58:44",lv:"err",l:"失败",m:"ADB 心跳：设备连接已断开（192.168.1.23:5555）"},
    {t:"10-24 11:59:16",lv:"ok",l:"成功",m:"ADB 心跳：设备已恢复连接（192.168.1.23:5555）"},
    {t:"10-24 18:00:05",lv:"ok",l:"成功",task:"日报截图",act:"截屏",m:"已保存 Screenshots/screencap_20261024_180005.png"},
    {t:"10-24 18:02:47",lv:"err",l:"失败",task:"周末巡检",act:"发送消息",m:"输入法未就绪，input text 未生效"},
    {t:"10-24 18:02:47",lv:"err",l:"失败",task:"周末巡检",m:"第 2 步失败，已中止（完成 1/3），邮件通知已发送"},
  ];
  let logsMore=[
    {t:"10-23 07:35:11",lv:"ok",l:"成功",task:"工作日打卡",m:"执行成功：完成 3/3 个动作，用时 41s"},
    {t:"10-23 09:00:14",lv:"err",l:"失败",task:"周末巡检",m:"触发时设备离线，已跳过本轮执行"},
  ];
  let filter="all",editIndex=-1,logExpanded=false;
  let randCfg={min:0,max:5}; // 全局随机偏移范围，与「调度 → 随机时间范围」设置联动

  const $=id=>document.getElementById(id);
  const VIEW_META={
    tasks:{title:"任务管理",desc:""},
    wifi:{title:"无线连接",desc:"Wi-Fi ADB · 仅支持无线"},
    sync:{title:"数据导入导出",desc:"配置备份与恢复"},
    apps:{title:"目标 APP",desc:"包名映射管理"},
    mail:{title:"邮箱配置",desc:"QQ 邮箱 · SMTP"},
    wecom:{title:"企业微信配置",desc:"群机器人 Webhook"},
    wait:{title:"任务等待时间",desc:"动作链步骤间隔"},
    reset:{title:"任务重置时间",desc:"每日任务链重置点"},
    random:{title:"随机时间范围",desc:"执行时间随机偏移"},
    switches:{title:"调度开关",desc:"随机任务 / 节假日 / 重置"},
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
  function addLog(lv,l,m,task,act){
    logs.push({t:nowStr(),lv,l,m,task:task||"",act:act||""});
    renderLogs();
    const list=$("logList");list.scrollTop=list.scrollHeight;
  }
  function renderLogs(){
    const list=logExpanded?logs.concat(logsMore):logs;
    $("logList").innerHTML=list.map(l=>`
      <div class="log-row"><span class="t">${l.t}</span><span class="task${l.task?"":" sys"}">${l.task||"系统"}</span><span class="lv ${l.lv}">${l.l}</span><span class="m">${l.act?`<span class="log-act">${l.act}</span>`:""}${l.m}</span></div>`).join("");
    $("logToggle").textContent=logExpanded?"收起":"查看全部";
  }
  $("logToggle").onclick=()=>{logExpanded=!logExpanded;renderLogs();};

  // ===== 任务表格 =====
  function chipsHtml(k){
    const acts=k.acts||[];
    if(!acts.length)return '<span class="chip warn">未配置动作</span>';
    const head=acts.slice(0,3).map(id=>{
      const a=actMeta(id);
      const tip=id==="SendMessage"&&k.payload?`${a.name}：${k.payload}`:`${a.name} · ${a.desc}`;
      return `<span class="chip blue" title="${tip}">${a.name}</span>`;
    });
    if(acts.length>3)head.push(`<span class="chip" title="${acts.slice(3).map(id=>actMeta(id).name).join(" › ")}">+${acts.length-3}</span>`);
    return `<div class="chips">${head.join("")}</div>`;
  }
  function render(){
    const list=tasks.filter(k=>filter==="all"||String(k.on)===(filter==="on"?"true":"false"));
    $("taskBody").innerHTML=list.length?list.map((k)=>{
      const i=tasks.indexOf(k);
      return `<tr data-i="${i}" class="${k.on?"":"disabled"}">
        <td><div class="task-name">${k.name}</div><div class="task-sub">${k.sub}</div></td>
        <td>${chipsHtml(k)}</td>
        <td>${k.time}</td>
        <td>${k.notify}</td>
        <td><button class="status-pill ${k.on?"on":"off"}" data-act="toggle"><span class="dot"></span>${k.on?"已启用":"已停用"}</button></td>
        <td><div class="ops">
          <a data-act="run" title="按当前动作链试运行一次，不影响调度计划">运行</a>
          <a data-act="edit">编辑</a>
          <a class="mute" data-act="toggle">${k.on?"暂停":"启用"}</a>
          <a class="danger" data-act="del">删除</a>
        </div></td>
      </tr>`;}).join("")
      :`<tr class="empty-row"><td colspan="6">暂无${filter==="off"?"已停用":"已启用"}任务，点击右上角「新建任务」添加</td></tr>`;
    $("countBadge").textContent=tasks.length+" 项";
  }
  $("taskBody").addEventListener("click",e=>{
    const act=e.target.dataset.act;
    if(!act)return;
    const tr=e.target.closest("tr");
    const i=+tr.dataset.i;
    if(act==="run")return runTask(i);
    if(act==="edit")return openDialog("edit",i);
    if(act==="toggle"){
      tasks[i].on=!tasks[i].on;
      toast("任务「"+tasks[i].name+"」已"+(tasks[i].on?"启用":"暂停"));
      return render();
    }
    if(act==="del"){
      tr.classList.add("removing");
      setTimeout(()=>{
        const name=tasks[i].name;
        tasks.splice(i,1);
        render();
        toast("任务「"+name+"」已删除");
      },180);
    }
  });

  // 试运行：按动作链逐步执行，动作级日志 + 末尾任务级汇总日志
  let runningTask=false;
  function runTask(i){
    const k=tasks[i];
    if(runningTask){toast("已有任务正在执行，请稍候");return;}
    if(!k.acts||!k.acts.length){toast("该任务未配置动作，无法执行");return;}
    if(k.acts.includes("SendMessage")&&!k.payload){toast("动作链含「发送消息」但未填写内容");return;}

    runningTask=true;
    toast("正在执行「"+k.name+"」");
    const steps=k.acts.slice();
    let n=0;
    const next=()=>{
      if(n>=steps.length){
        addLog("ok","成功",`执行成功：完成 ${steps.length}/${steps.length} 个动作`,k.name);
        toast("「"+k.name+"」执行完成");
        runningTask=false;return;
      }
      const id=steps[n++],a=actMeta(id);
      const appName=(k.sub||"").split(" · ")[0]||"APP";
      const detail=id==="SendMessage"?`已发送「${k.payload}」`
        :id==="OpenTarget"?`已启动 ${appName}`
        :id==="KillTarget"?`已关闭 ${appName}`
        :id==="ScreenCapture"?`已保存 Screenshots/screencap_${Date.now()}.png`
        :a.desc;
      addLog("ok","成功",detail,k.name,a.name);
      setTimeout(next,1100);
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
    if(!offsetEnabled){el.textContent="到达设定时间后立即执行，不做随机偏移";return;}
    const lo=randCfg.min,hi=randCfg.max;
    if(lo>hi){el.textContent="随机时间范围设置有误：下限不能大于上限";return;}
    const mm=/^(\d{1,2}):(\d{1,2})$/.exec(v);
    if(!mm){el.textContent=`实际执行时间 = 设定时间 + ${lo}~${hi} 分钟（输入时间后预览）`;return;}
    el.textContent=`实际执行时间：${fmtHM(+mm[1]*60+ +mm[2]+lo)} ~ ${fmtHM(+mm[1]*60+ +mm[2]+hi)} 之间随机`;
  }
  function refreshOffsetUI(){
    $("offsetSwitch").classList.toggle("on",offsetEnabled);
    updateOffsetHint();
  }
  function fillAppOptions(selectedPkg){
    $("fApp").innerHTML='<option value="">请选择目标 APP</option>'
      +apps.map(a=>`<option value="${a.pkg}" ${a.pkg===selectedPkg?"selected":""}>${a.name} · ${a.pkg}</option>`).join("");
  }
  // 动作多选：勾选集合始终按目录顺序排列，序号即执行顺序
  let draftActs=[];
  function renderActs(){
    $("fActs").innerHTML=ACTIONS.map(a=>{
      const idx=draftActs.indexOf(a.id),on=idx>=0;
      const need=a.needPayload?'<span class="need">需填写内容</span>':"";
      return `<div class="act-item${on?" on":""}" data-id="${a.id}" title="${a.cmd}">
        <span class="act-order">${on?idx+1:""}</span>
        <div class="act-body">
          <div class="act-name">${a.name}</div>
          <div class="act-desc">${a.desc}${need}</div>
        </div>
      </div>`;
    }).join("");

    $("fPayloadWrap").classList.toggle("show",draftActs.includes("SendMessage"));
    $("actSummary").innerHTML=draftActs.length
      ? `${draftActs.map(id=>`<span class="chip blue">${actMeta(id).name}</span>`).join('<span class="arrow">›</span>')}<span class="count">共 ${draftActs.length} 个动作</span>`
      : `<span class="empty">尚未选择动作 · 至少选择 1 个才能保存</span>`;
  }
  $("fActs").addEventListener("click",e=>{
    const item=e.target.closest(".act-item");
    if(!item)return;
    const id=item.dataset.id;
    const i=draftActs.indexOf(id);
    if(i>=0)draftActs.splice(i,1);
    else draftActs.push(id);
    draftActs.sort(actSort);
    renderActs();
  });

  function openDialog(mode,i){
    editIndex=(mode==="edit")?i:-1;
    $("dlgTitle").textContent=mode==="edit"?"编辑任务":"新建任务";
    $("fName").textContent=mode==="edit"?tasks[i].name:"";
    const t=mode==="edit"?tasks[i].time:"";
    $("fTime").textContent=t.split(" ")[0];
    offsetEnabled=mode==="edit"?parseOffset(t):true;
    fillAppOptions(mode==="edit"?(tasks[i].sub.split(" · ")[1]||""):"");

    // 新建默认「亮屏 + 打开应用」，与旧版本任务的迁移结果保持一致
    draftActs=mode==="edit"?(tasks[i].acts||[]).slice():["ScreenOn","OpenTarget"];
    $("fPayload").value=mode==="edit"?(tasks[i].payload||""):"";
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
    if(!draftActs.length){toast("请至少选择一个任务动作");return;}
    if(draftActs.includes("SendMessage")&&!$("fPayload").value.trim()){toast("请输入要发送的消息内容");return;}

    const app=apps.find(a=>a.pkg===pkg);
    const sub=app?`${app.name} · ${app.pkg}`:"";
    const label=time+offsetSuffix();
    const acts=draftActs.slice();
    const payload=draftActs.includes("SendMessage")?$("fPayload").value.trim():"";
    const chain=acts.map(id=>actMeta(id).name).join(" → ");

    if(editIndex>=0){
      Object.assign(tasks[editIndex],{name,time:label,sub,on:true,acts,payload});
      toast("任务「"+name+"」已保存");
      addLog("info","信息","动作链已更新：" + chain,name);
    }else{
      tasks.unshift({name,sub,time:label,notify:"邮件",on:true,acts,payload});
      toast("任务「"+name+"」已创建");
      addLog("info","信息","已加入调度队列，动作链：" + chain,name);
    }
    closeDialog();render();
  };

  // ===== 目标 APP 管理 =====
  function renderApps(){
    $("appBody").innerHTML=apps.length?apps.map((a,i)=>`
      <tr data-i="${i}">
        <td><div class="task-name">${a.name}</div></td>
        <td><span class="pkg">${a.pkg}</span></td>
        <td style="text-align:right;"><div class="ops" style="justify-content:flex-end;"><a class="danger" data-act="delApp">删除</a></div></td>
      </tr>`).join("")
      :`<tr class="empty-row"><td colspan="3">暂无映射，在下方输入应用名称与包名后点击「添加」</td></tr>`;
    $("appCount").textContent=apps.length+" 项";
  }
  $("appBody").addEventListener("click",e=>{
    if(e.target.dataset.act!=="delApp")return;
    const i=+e.target.closest("tr").dataset.i;
    const name=apps[i].name;
    apps.splice(i,1);renderApps();
    toast("已移除「"+name+"」包名映射");
  });
  $("addApp").onclick=()=>{
    const n=$("appName").value.trim(),p=$("appPkg").value.trim();
    if(!n||!p){toast("请填写应用名称与包名");return;}
    if(!/^[a-zA-Z][\w]*(\.[\w]+)+$/.test(p)){toast("包名格式不正确，如 com.example.app");return;}
    apps.push({name:n,pkg:p});
    $("appName").value="";$("appPkg").value="";
    renderApps();toast("已添加「"+n+"」映射");
    addLog("info","信息","新增包名映射："+n+" · "+p);
  };
  $("importApps").onclick=()=>{toast("已从 apps.csv 导入 3 条包名映射（演示）");};
  renderApps();

  // ===== 数据导入导出 =====
  document.querySelectorAll("[data-io]").forEach(b=>b.addEventListener("click",()=>{
    const io=b.dataset.io;
    if(io==="export-tasks"){toast("任务配置已导出：tasks_2026-10-24.csv");addLog("ok","成功","任务配置已导出为 tasks_2026-10-24.csv");}
    if(io==="export-all"){toast("全部配置已导出：config_2026-10-24.csv");addLog("ok","成功","全部配置（任务/映射/通知）已导出");}
    if(io==="import-tasks"){toast("已从 CSV 导入 4 条任务（演示）");addLog("ok","成功","从 CSV 导入 4 条任务配置");}
    if(io==="import-apps"){toast("已从 CSV 导入 3 条包名映射（演示）");}
  }));

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

  // 节假日同步
  $("syncHoliday").onclick=()=>{
    toast("节假日数据同步完成");
    $("holidaySyncTime").textContent=nowStr().replace("-","-")+" · 覆盖 2026 全年";
    addLog("ok","成功","节假日数据已从 chinese-days CDN 同步（覆盖 2026 全年）");
  };

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
      st.textContent="Pixel 6 · "+full+" · 已连接 · 延迟 "+Math.floor(4+Math.random()*12)+"ms";
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
  // 真实实现：30s 一次 adb devices 探测，连续 2 次失败判定掉线，90s 无成功探测视为停滞；
  // 心跳本身只在「掉线 / 恢复」时写运行日志，界面靠这里的秒数表达"心还在跳"。
  // 原型演示加速：4s 一次成功探测，12s 判定停滞。
  const HB_DEMO_TICK = 4;
  const HB_DEMO_STALE = 12;
  let hbLast = Date.now(), hbFrozen = false, hbDropLogged = false;

  const hbElapsed = () => Math.floor((Date.now() - hbLast) / 1000);

  function hbReset() {
    hbLast = Date.now();
    hbFrozen = false;
    hbDropLogged = false;
    $("btnSimDrop").textContent = "模拟掉线";
    renderHeartbeat();
  }

  function hbStop() {
    hbFrozen = false;
    hbDropLogged = false;
    $("btnSimDrop").textContent = "模拟掉线";
    renderHeartbeat();
  }

  function renderHeartbeat() {
    const elapsed = hbElapsed();
    const stalled = wifiConnected && hbFrozen && elapsed >= HB_DEMO_STALE;
    const beating = wifiConnected && !stalled;
    const cls = !wifiConnected ? "" : stalled ? "stalled" : "alive";

    const t = $("adbHbText");
    t.className = "hb-text " + cls;
    t.textContent = !wifiConnected
      ? "心跳 -"
      : stalled
        ? `停滞 ${elapsed < 60 ? elapsed + "s" : Math.floor(elapsed / 60) + "min"}`
        : `心跳 ${elapsed}s`;

    // 单一状态点：颜色表示连接状态，动画表示心跳节奏
    const dot = $("adbDot");
    dot.classList.toggle("beat", beating);
    dot.classList.toggle("off", !wifiConnected);
    dot.classList.toggle("bad", stalled);
  }

  function restoreLinkUi() {
    $("adbStatus").textContent = "ADB 已连接";
    $("adbStatus").style.color = "var(--green)";
    $("wifiDot").classList.remove("off");
    $("wifiDot").classList.add("pulse");
    $("wifiState").textContent = "Pixel 6 · " + CONN_DEFAULT + " · 已连接 · 延迟 9ms";
    $("wifiState").style.color = "var(--text-1)";
  }

  // 心跳判定掉线：只归位界面状态 + 写一条翻转日志，不弹 Toast（无人值守场景不打扰）
  function hbJudgeDrop() {
    hbDropLogged = true;
    addLog("err", "失败", "ADB 心跳：设备连接已断开（" + CONN_DEFAULT + "）");
    $("adbStatus").textContent = "ADB 未连接";
    $("adbStatus").style.color = "var(--red)";
    $("wifiDot").classList.remove("pulse");
    $("wifiDot").classList.add("off");
    $("wifiState").textContent = "未连接设备 · 心跳判定掉线，正在自动重连…";
    $("wifiState").style.color = "var(--text-3)";
    renderHeartbeat();

    // 模拟 adb connect 自动重连（真实实现最多等待一个心跳周期）
    setTimeout(() => {
      if (!wifiConnected) return;
      hbFrozen = false;
      hbDropLogged = false;
      hbLast = Date.now();
      $("btnSimDrop").textContent = "模拟掉线";
      restoreLinkUi();
      addLog("ok", "成功", "ADB 心跳：设备已恢复连接（" + CONN_DEFAULT + "）");
      renderHeartbeat();
    }, 2000);
  }

  // 每秒刷新"心跳 N 秒前"；每 4s 模拟一次成功探测
  setInterval(() => {
    if (wifiConnected && !hbFrozen && Date.now() - hbLast >= HB_DEMO_TICK * 1000) hbLast = Date.now();
    if (wifiConnected && hbFrozen && !hbDropLogged && hbElapsed() >= HB_DEMO_STALE) hbJudgeDrop();
    renderHeartbeat();
  }, 1000);

  $("btnSimDrop").onclick = () => {
    if (!wifiConnected) { toast("请先在下方连接设备"); return; }
    if (!hbFrozen) {
      hbFrozen = true;
      hbLast = Date.now();
      $("btnSimDrop").textContent = "恢复连接";
      toast("已模拟掉线：心跳时间冻结，12s 后判定停滞");
      renderHeartbeat();
    } else {
      hbFrozen = false;
      hbDropLogged = false;
      hbLast = Date.now();
      $("btnSimDrop").textContent = "模拟掉线";
      restoreLinkUi();
      addLog("ok", "成功", "ADB 心跳：设备已恢复连接（" + CONN_DEFAULT + "）");
      toast("心跳已恢复");
      renderHeartbeat();
    }
  };

  // ===== 初始化 =====
  // 演示初始态：默认设备已连接，便于直接观察心跳跳动（不自动跳转页面）
  render();renderLogs();
  wifiConnected = true;
  restoreLinkUi();
  $("svcBadge").style.display = "";
  refreshLock();
  hbReset();
