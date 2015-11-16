var app = require('app');  // Module to control application life.
var BrowserWindow = require('browser-window');  // Module to create native browser window.
var WealthPulseServer = require('./wealthpulseserver');

// Report crashes to our server.
//require('crash-reporter').start();

// Keep a global reference of the window object, if you don't, the window will
// be closed automatically when the JavaScript object is garbage collected.
var mainWindow = null;

// Quit when all windows are closed.
app.on('window-all-closed', function() {
  // On OS X it is common for applications and their menu bar
  // to stay active until the user quits explicitly with Cmd + Q
  // but in our case we will just quit
  app.quit();
});

// This method will be called when Electron has finished
// initialization and is ready to create browser windows.
app.on('ready', function() {
  // Spawn WealthPulse server
  var server = new WealthPulseServer();

  server.on('ready', function (location) {
    // Create the browser window.
    mainWindow = new BrowserWindow({width: 1200, height: 820, 'node-integration': false});

    // and load the url of the WealthPulse server
    mainWindow.loadUrl(location);

    // Open the DevTools.
    //mainWindow.openDevTools();

    // Emitted when the window is closed.
    mainWindow.on('closed', function() {
      // Dereference the window object, usually you would store windows
      // in an array if your app supports multi windows, this is the time
      // when you should delete the corresponding element.
      mainWindow = null;

      // Kill the WealthPulse server process
      server.kill();
    });
  });

  server.spawn('mono', ['WealthPulse/bin/Debug/wealthpulse.exe']);
});
