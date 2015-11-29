var util = require('util');
var spawn = require('child_process').spawn;
var EventEmitter = require('events').EventEmitter;

function WealthPulseServerController () {
  EventEmitter.call(this);
  this.serverProcess = null;
}

util.inherits(WealthPulseServerController, EventEmitter);

WealthPulseServerController.prototype.spawn = function (command, args, options) {
  this.serverProcess = spawn(command, args, options);

  var self = this;

  this.serverProcess.stdout.on('data', function (data) {
    // pipe straight through to stdout
    process.stdout.write(data);

    // listen until we get the server location, then emit
    // that the server is ready with its location
    var dataString = data.toString();
    var locationMessagePrefix = 'WealthPulse server running at ';

    if (dataString.indexOf(locationMessagePrefix) >= 0) {
      var location = dataString.substr(locationMessagePrefix.length);
      self.emit('ready', location);
    }
  });
};

WealthPulseServerController.prototype.kill = function (signal) {
  this.serverProcess.kill(signal);
  this.serverProcess = null;
}

module.exports = WealthPulseServerController;