  // ===== 数据 =====
  const DEVICE="Pixel 6";
  const CHIPS=["唤醒设备","打开App","屏幕截屏","发送通知"];
  let tasks=[
    {name:"工作日打卡",sub:"钉钉 · com.alibaba.android.rimet",time:"07:30 ±5min",notify:"邮件 + 企微",on:true},
    {name:"会议提醒",sub:"飞书 · com.ss.android.lark",time:"08:50 ±3min",notify:"企业微信",on:true},
    {name:"日报截图",sub:"企业微信 · com.tencent.wework",time:"18:00",notify:"邮件",on:true},
    {name:"周末巡检",sub:"设备状态检查 · 截屏留档",time:"09:00 ±10min",notify:"邮件",on:false},
  ];
  let apps=[
    {name:"钉钉",pkg:"com.alibaba.android.rimet"},
    {name:"飞书",pkg:"com.ss.android.lark"},
    {name:"企业微信",pkg:"com.tencent.wework"},
  ];
  let logs=[
    {t:"10-24 07:35:02",lv:"ok",l:"成功",m:"「工作日打卡」唤醒 → 打开钉钉 → 截屏，邮件与企微通知已送达"},
    {t:"10-24 08:52:10",lv:"ok",l:"成功",m:"「会议提醒」唤醒设备并打开飞书，企微通知已送达"},
    {t:"10-24 12:00:31",lv:"info",l:"信息",m:"ADB 心跳正常，Pixel 6 在线"},
    {t:"10-24 18:00:05",lv:"ok",l:"成功",m:"「日报截图」截屏完成，截屏文件已作为邮件附件发送"},
    {t:"10-24 18:02:47",lv:"err",l:"失败",m:"「周末巡检」ADB 连接超时，已跳过本轮执行并记录"},
  ];
  let logsMore=[
    {t:"10-23 07:35:11",lv:"ok",l:"成功",m:"「工作日打卡」执行完成，用时 42s"},
    {t:"10-23 09:00:14",lv:"err",l:"失败",m:"「周末巡检」设备未授权 USB 调试，重试 2 次后跳过"},
  ];
  let filter="all",editIndex=-1,logExpanded=false;

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

  // Ctrl+T / Ctrl+R 快捷键
  document.addEventListener("keydown",e=>{
    if(!e.ctrlKey||e.shiftKey||e.altKey)return;
    const k=e.key.toLowerCase();
    if(k==="t"){e.preventDefault();switchView("wait");setTimeout(()=>$("waitInput").focus(),60);toast("已定位到「任务等待时间」");}
    if(k==="r"){e.preventDefault();switchView("reset");setTimeout(()=>$("resetInput").focus(),60);toast("已定位到「任务重置时间」");}
  });

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
  function render(){
    const list=tasks.filter(k=>filter==="all"||String(k.on)===(filter==="on"?"true":"false"));
    $("taskBody").innerHTML=list.length?list.map((k)=>{
      const i=tasks.indexOf(k);
      return `<tr data-i="${i}" class="${k.on?"":"disabled"}">
        <td><div class="task-name">${k.name}</div><div class="task-sub">${k.sub}</div></td>
        <td>${k.time}</td>
        <td><div class="chips">${CHIPS.map(c=>`<span class="chip">${c}</span>`).join("")}</div></td>
        <td>${k.notify}</td>
        <td><button class="status-pill ${k.on?"on":"off"}" data-act="toggle"><span class="dot"></span>${k.on?"已启用":"已停用"}</button></td>
        <td><div class="ops">
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

  // 筛选
  document.querySelectorAll(".filters span").forEach(f=>f.addEventListener("click",()=>{
    document.querySelectorAll(".filters span").forEach(x=>x.classList.remove("on"));
    f.classList.add("on");filter=f.dataset.f;render();
  }));

  // ===== 任务弹窗 =====
  const overlay=$("overlay");
  function openDialog(mode,i){
    editIndex=(mode==="edit")?i:-1;
    $("dlgTitle").textContent=mode==="edit"?"编辑任务":"新建任务";
    $("fName").textContent=mode==="edit"?tasks[i].name:"";
    $("fTime").textContent=mode==="edit"?tasks[i].time.split(" ")[0]:"";
    overlay.classList.add("show");
    setTimeout(()=>$("fName").focus(),50);
  }
  function closeDialog(){overlay.classList.remove("show");}
  $("btnNew").onclick=()=>openDialog("new");
  $("btnClose").onclick=closeDialog;
  $("btnCancel").onclick=closeDialog;
  overlay.addEventListener("click",e=>{if(e.target===overlay)closeDialog();});
  document.addEventListener("keydown",e=>{if(e.key==="Escape")closeDialog();});
  $("btnSave").onclick=()=>{
    const name=$("fName").textContent.trim()||"未命名任务";
    const time=$("fTime").textContent.trim()||"08:00";
    if(editIndex>=0){
      tasks[editIndex].name=name;
      tasks[editIndex].time=time+" ±5min";
      tasks[editIndex].on=true;
      toast("任务「"+name+"」已保存");
      addLog("info","信息","任务「"+name+"」配置已更新");
    }else{
      tasks.unshift({name,sub:"单设备 · "+DEVICE,time:time+" ±5min",notify:"邮件",on:true});
      toast("任务「"+name+"」已创建");
      addLog("info","信息","新任务「"+name+"」已加入调度队列");
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
    const m={mail:"邮箱配置",wecom:"企业微信配置",wait:"任务等待时间",reset:"任务重置时间",random:"随机时间范围"}[b.dataset.save];
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
  const CMD_LINES={
    wake:["$ adb shell input keyevent KEYCODE_WAKEUP","$ adb shell wm dismiss-keyguard"],
    off:["$ adb shell input keyevent KEYCODE_SLEEP"],
    cap:["$ adb exec-out screencap -p > screen_1030.png"],
    open:["$ adb shell am start -n com.alibaba.android.rimet/com.alibaba.android.rimet.biz.LaunchHomeActivity"],
    close:["$ adb shell am force-stop com.alibaba.android.rimet"],
  };
  function cmdOut(html){
    const el=$("cmdOut");
    el.insertAdjacentHTML("beforeend",html);
    el.scrollTop=el.scrollHeight;
  }
  document.querySelectorAll(".cmd-btn").forEach(b=>b.addEventListener("click",()=>{
    const lines=CMD_LINES[b.dataset.cmd];
    lines.forEach((l,i)=>setTimeout(()=>cmdOut(`<div>${l}</div>`),i*220));
    const done=()=>{
      const ok=b.dataset.cmd!=="adb";
      cmdOut(`<div class="${ok?"ok":"err"}">→ 执行成功（${Math.floor(120+Math.random()*400)}ms）</div>`);
      const names={wake:"亮屏",off:"息屏",cap:"截屏",open:"打开目标应用",close:"关闭目标应用"};
      addLog(ok?"ok":"ok","成功","指令测试 · "+names[b.dataset.cmd]+"执行成功");
    };
    setTimeout(done,lines.length*220+320);
    toast("指令已下发");
  }));
  $("clearCmd").onclick=()=>{
    $("cmdOut").innerHTML=`<span class="c">── ADB 指令控制台（输出仅作原型演示） ──</span>`;
    toast("控制台输出已清空");
  };

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
    const ad=$("adbDot"),as=$("adbStatus"),dv=$("adbDev");
    if(on){
      dot.classList.add("pulse");dot.classList.remove("off");
      st.textContent="Pixel 6 · "+full+" · 已连接 · 延迟 "+Math.floor(4+Math.random()*12)+"ms";
      st.style.color="var(--text-1)";
      ad.classList.add("pulse");ad.classList.remove("off");
      as.textContent="ADB 已连接";as.style.color="var(--green)";
      dv.textContent="Pixel 6 · "+full;
      $("svcBadge").style.display="";
      refreshLock();
      if(curView==="wifi")switchView("tasks");
    }else{
      dot.classList.remove("pulse");dot.classList.add("off");
      st.textContent="未连接设备 · 请在下方完成配对与连接";
      st.style.color="var(--text-3)";
      ad.classList.remove("pulse");ad.classList.add("off");
      as.textContent="ADB 未连接";as.style.color="var(--red)";
      dv.textContent="等待无线连接";
      $("svcBadge").style.display="none";
      refreshLock();
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

  // ===== 初始化 =====
  render();renderLogs();refreshLock();
