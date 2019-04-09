/************************************************************************************************************************************************
 *  BreezeD command line arguments
 ************************************************************************************************************************************************
 *	 -testnet			: Use testnet network; peers should be discovered automatically
 *	 -regtest			: Use testnet network; you need to use addnode or connect in the stratis.conf or bitcoin.conf to connect to any peers
 *   -noDaemons			: Do not start the BreezeD daemons for stratis and bitcoin networks
 *	 -noTor				: Disable Tor and use TCP connection handler; only works for regtest network
 *	 -tumblerProtocol	: Can be set to TCP (default) or Http. The Http can only be used when noTor switch is used as well
 *	 -dataDir			: Node's data directory; this option is passed to the BreezeD as -datadir
 *	 -bitcoinPort		: Bitcoin protocol port; this is passed to the BreezeD as -port
 *	 -stratisPort		: Stratis protocol port; this is passed to the BreezeD as -port
 *	 -bitcoinApiPort	: Bitcoin API port; this is passed to the BreezeD as -apiport
 *	 -stratisApiPort	: Stratis API port; this is passed to the BreezeD as -apiport
 *   -storeDir			: Location of the registrationHistory.json file; this is passed to the BreezeD as -storeDir
 ************************************************************************************************************************************************/

import { app, BrowserWindow, ipcMain, Menu, nativeImage, Tray } from 'electron';
import * as path from 'path';
import * as url from 'url';
import * as os from 'os';
if (os.arch() == 'arm') {
  app.disableHardwareAcceleration();
}

let serve;
let testnet = false;
let regtest = false;
let noTor;
let tumblerProtocol;
let dataDir;
let storeDir;
let bitcoinPort;
let stratisPort;
let startDaemons;
(<any>global).bitcoinApiPort = 37220;
(<any>global).stratisApiPort = 37221;
const args = process.argv.slice(1);
serve = args.some(val => val === '--serve' || val === '-serve');
startDaemons = !args.some(val => val === '--noDaemons' || val === '-noDaemons');

if (args.some(val => val === '--testnet' || val === '-testnet')) {
  testnet = true;
  (<any>global).bitcoinApiPort = 38220;
  (<any>global).stratisApiPort = 38221;
} else if (args.some(val => val === '--regtest' || val === '-regtest')) {
  regtest = true;
  (<any>global).bitcoinApiPort = 37220;
  (<any>global).stratisApiPort = 37221;
} else if (args.some(val => val === '--mainnet' || val === '-mainnet')) {
  (<any>global).bitcoinApiPort = 37220;
  (<any>global).stratisApiPort = 37221;
}

//Set custom blockchain protocol ports
if (args.some(val => val.indexOf("--bitcoinPort=") == 0 || val.indexOf("-bitcoinPort=") == 0)) {
	bitcoinPort = args.filter(val => val.indexOf("--bitcoinPort=") == 0 || val.indexOf("-bitcoinPort=") == 0)[0].split("=")[1];
}
if (args.some(val => val.indexOf("--stratisPort=") == 0 || val.indexOf("-stratisPort=") == 0)) {
	stratisPort = args.filter(val => val.indexOf("--stratisPort=") == 0 || val.indexOf("-stratisPort=") == 0)[0].split("=")[1];
}

//Set custom API ports
if (args.some(val => val.indexOf("--bitcoinApiPort=") == 0 || val.indexOf("-bitcoinApiPort=") == 0)) {
	let customBitcoinApiPort : string;
	customBitcoinApiPort = args.filter(val => val.indexOf("--bitcoinApiPort=") == 0 || val.indexOf("-bitcoinApiPort=") == 0)[0].split("=")[1];
	(<any>global).bitcoinApiPort = customBitcoinApiPort;
}
if (args.some(val => val.indexOf("--stratisApiPort=") == 0 || val.indexOf("-stratisApiPort=") == 0)) {
	let customStratisApiPort : string;
	customStratisApiPort = args.filter(val => val.indexOf("--stratisApiPort=") == 0 || val.indexOf("-stratisApiPort=") == 0)[0].split("=")[1];
	(<any>global).stratisApiPort = customStratisApiPort;
}

//Set datadir and storedir parameters
if (args.some(val => val.indexOf("--datadir=") == 0 || val.indexOf("-datadir=") == 0)) {
	dataDir = args.filter(val => val.indexOf("--datadir=") == 0 || val.indexOf("-datadir=") == 0)[0].split("=")[1];
}
if (args.some(val => val.indexOf("--storedir=") == 0 || val.indexOf("-storedir=") == 0)) {
	storeDir = args.filter(val => val.indexOf("--storedir=") == 0 || val.indexOf("-storedir=") == 0)[0].split("=")[1];
}

//Set Regtest Tor and Tumbling protocol settings
noTor = args.some(val => val === '--noTor' || val === '-noTor');
if (args.some(val => val.indexOf("--tumblerProtocol=") == 0 || val.indexOf("-tumblerProtocol=") == 0)) {
	tumblerProtocol = args.filter(val => val.indexOf("--tumblerProtocol=") == 0 || val.indexOf("-tumblerProtocol=") == 0)[0].split("=")[1];
}

ipcMain.on('get-testnet', (event, arg) => {
  event.returnValue = testnet;
});

require('electron-context-menu')({
  showInspectElement: serve
});

// Keep a global reference of the window object, if you don't, the window will
// be closed automatically when the JavaScript object is garbage collected.
let mainWindow = null;

function createWindow() {
  // Create the browser window.
  mainWindow = new BrowserWindow({
    width: 1200,
    height: 650,
    frame: true,
    minWidth: 1200,
    minHeight: 650,
    title: 'Breeze'
  });

  if (serve) {
    require('electron-reload')(__dirname, { });
    mainWindow.loadURL('http://localhost:4200');
  } else {
    mainWindow.loadURL(url.format({
      pathname: path.join(__dirname, 'dist/index.html'),
      protocol: 'file:',
      slashes: true
    }));
  }

  if (serve) {
    mainWindow.webContents.openDevTools();
  }

  // Emitted when the window is closed.
  mainWindow.on('closed', function () {
    // Dereference the window object, usually you would store windows
    // in an array if your app supports multi windows, this is the time
    // when you should delete the corresponding element.
    mainWindow = null;
  });

  // Emitted when the window is going to close.
  mainWindow.on('close', function () {
  })
};

// This method will be called when Electron has finished
// initialization and is ready to create browser windows.
// Some APIs can only be used after this event occurs.
app.on('ready', function () {
  if (serve) {
    console.log('Breeze UI was started in development mode. This requires the user to be running the Breeze Daemon himself.')
  } else if (startDaemons) {
    startBitcoinDaemon();
    startStratisDaemon();
  }
  createTray();
  createWindow();
  if (os.platform() === 'darwin') {
    createMenu();
  }
});

// Quit when all windows are closed.
app.on('window-all-closed', function () {
  //The user doesn't have the option to create another window/wallet from the Electron menu, so there's
  //no point in keeping it there, so we simply quit the app.
  quit();
});

app.on('activate', function () {
  // On OS X it's common to re-create a window in the app when the
  // dock icon is clicked and there are no other windows open.
  if (mainWindow === null) {
    createWindow();
  }
});

function closeBitcoinDaemon() {
  let http = require('http');
  let body = JSON.stringify({});

  let request = new http.ClientRequest({
    method: 'POST',
    hostname: 'localhost',
    port: (<any>global).bitcoinApiPort,
    path: '/api/node/shutdown',
    headers: {
      "Content-Type": "application/json",
      "Content-Length": Buffer.byteLength(body)
    }
  })

  request.write('true');
  request.on('error', function (e) { });
  request.on('timeout', function (e) { request.abort(); });
  request.on('uncaughtException', function (e) { request.abort(); });
  request.end(body);
};

function closeStratisDaemon() {
  let http = require('http');
  let body = JSON.stringify({});

  let request = new http.ClientRequest({
    method: 'POST',
    hostname: 'localhost',
    port: (<any>global).stratisApiPort,
    path: '/api/node/shutdown',
    headers: {
      "Content-Type": "application/json",
      "Content-Length": Buffer.byteLength(body)
    }
  })

  request.write('true');
  request.on('error', function (e) { });
  request.on('timeout', function (e) { request.abort(); });
  request.on('uncaughtException', function (e) { request.abort(); });
  request.end(body);
};

function startBitcoinDaemon() {
  let bitcoinProcess;
  const spawnBitcoin = require('child_process').spawn;

  // Start Breeze Bitcoin Daemon
  let daemonPath;
  if (os.platform() === 'win32') {
      daemonPath = path.resolve(__dirname, '..\\..\\resources\\daemon\\Breeze.Daemon.exe');
  } else if (os.platform() === 'linux') {
    daemonPath = path.resolve(__dirname, '..//..//resources//daemon//Breeze.Daemon');
  } else {
    daemonPath = path.resolve(__dirname, '..//..//resources//daemon//Breeze.Daemon');
  }

   let commandLineArguments = [];
   commandLineArguments.push("-light");
   commandLineArguments.push("-apiport=" + (<any>global).bitcoinApiPort);
   if(bitcoinPort != null)
	 commandLineArguments.push("-port=" + bitcoinPort);

   if(testnet)
	 commandLineArguments.push("-testnet");
   if(regtest)
	 commandLineArguments.push("-regtest");

   if (noTor)
	 commandLineArguments.push("-noTor");

   if (tumblerProtocol != null)
	 commandLineArguments.push("-tumblerProtocol=" + tumblerProtocol);

   commandLineArguments.push("-tumblebit");
   commandLineArguments.push("-registration");
   if (dataDir != null)
     commandLineArguments.push("-datadir=" + dataDir);

   if (storeDir != null)
     commandLineArguments.push("-storedir=" + storeDir);

   console.log("Starting Bitcoin daemon with parameters: " + commandLineArguments);
   bitcoinProcess = spawnBitcoin(daemonPath, commandLineArguments, {
      detached: false
    });

  bitcoinProcess.stdout.on('data', (data) => {
    writeLog(`Bitcoin: ${data}`);
  });
}

function startStratisDaemon() {
  let stratisProcess;
  const spawnStratis = require('child_process').spawn;

  // Start Breeze Stratis Daemon
  let daemonPath = path.resolve(__dirname, 'assets//daemon//Breeze.Daemon');
  if (os.platform() === 'win32') {
      daemonPath = path.resolve(__dirname, '..\\..\\resources\\daemon\\Breeze.Daemon.exe');
  } else if (os.platform() === 'linux') {
    daemonPath = path.resolve(__dirname, '..//..//resources//daemon//Breeze.Daemon');
  } else {
    daemonPath = path.resolve(__dirname, '..//..//resources//daemon//Breeze.Daemon');
  }

  let commandLineArguments = [];
  commandLineArguments.push("-stratis");
  commandLineArguments.push("-apiport=" + (<any>global).stratisApiPort);
   if(stratisPort != null)
	 commandLineArguments.push("-port=" + stratisPort);

  commandLineArguments.push("-light");
  if(testnet)
	commandLineArguments.push("-testnet");
  if(regtest)
    commandLineArguments.push("-regtest");

  commandLineArguments.push("-registration");
  if (dataDir != null)
	commandLineArguments.push("-datadir=" + dataDir);

  console.log("Starting Stratis daemon with parameters: " + commandLineArguments);
  stratisProcess = spawnStratis(daemonPath, commandLineArguments, {
    detached: false
  });

  stratisProcess.stdout.on('data', (data) => {
    writeLog(`Stratis: ${data}`);
  });
}

function createTray() {
  // Put the app in system tray
  let trayIcon;
  if (serve) {
    trayIcon = nativeImage.createFromPath('./src/assets/images/breeze-logo-tray.png');
  } else {
    trayIcon = nativeImage.createFromPath(path.resolve(__dirname, '../../resources/src/assets/images/breeze-logo-tray.png'));
  }

  const systemTray = new Tray(trayIcon);
  const contextMenu = Menu.buildFromTemplate([
    {
      label: 'Hide/Show',
      click: function() {
        mainWindow.isVisible() ? mainWindow.hide() : mainWindow.show();
      }
    },
    {
      label: 'Exit',
      click: function() {
        quit();
      }
    }
  ]);
  systemTray.setToolTip('Breeze Wallet');
  systemTray.setContextMenu(contextMenu);
  systemTray.on('click', function() {
    if (!mainWindow.isVisible()) {
      mainWindow.show();
    }

    if (!mainWindow.isFocused()) {
      mainWindow.focus();
    }
  });

  app.on('window-all-closed', function () {
    if (systemTray) {systemTray.destroy(); }
  });
};

function writeLog(msg) {
  console.log(msg);
};

function createMenu() {
  const menuTemplate = [{
    label: app.getName(),
    submenu: [
      { label: "About " + app.getName(), selector: "orderFrontStandardAboutPanel:" },
      { label: "Quit", accelerator: "Command+Q", click: function() { app.quit(); }}
    ]}, {
    label: "Edit",
    submenu: [
      { label: "Undo", accelerator: "CmdOrCtrl+Z", selector: "undo:" },
      { label: "Redo", accelerator: "Shift+CmdOrCtrl+Z", selector: "redo:" },
      { label: "Cut", accelerator: "CmdOrCtrl+X", selector: "cut:" },
      { label: "Copy", accelerator: "CmdOrCtrl+C", selector: "copy:" },
      { label: "Paste", accelerator: "CmdOrCtrl+V", selector: "paste:" },
      { label: "Select All", accelerator: "CmdOrCtrl+A", selector: "selectAll:" }
    ]}
  ];

  Menu.setApplicationMenu(Menu.buildFromTemplate(menuTemplate));
};

const quit = () => {
  closeBitcoinDaemon();
  closeStratisDaemon();
  app.quit();
};
