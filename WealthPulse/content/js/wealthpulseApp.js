angular.module('wealthpulseApp', ['ngRoute', 'wpLineChart'])
	.config(function($routeProvider) {
		$routeProvider
			.when('/balance', {
				controller: BalanceCtrl,
				templateUrl: '/content/partials/balance.html',
				resolve: BalanceCtrl.resolve
			})
			.when('/networth', {
				controller: NetWorthCtrl,
				templateUrl: '/content/partials/linechart.html',
				resolve: NetWorthCtrl.resolve
			})
			.when('/currentincomestatement', {
				controller: CurrentIncomeStatementCtrl,
				templateUrl: '/content/partials/balance.html',
				resolve: CurrentIncomeStatementCtrl.resolve
			})
			.when('/previousincomestatement', {
				controller: PreviousIncomeStatementCtrl,
				templateUrl: '/content/partials/balance.html',
				resolve: PreviousIncomeStatementCtrl.resolve
			})
			.otherwise({redirectTo: '/balance'});
	});


function BalanceCtrl($scope, response) {
	$scope.balance = response.data;
}

BalanceCtrl.resolve = {
	response: function($q, $http) {
		return $http.get('/api/balance.json');
	}
}


function CurrentIncomeStatementCtrl($scope, response) {
	$scope.balance = response.data;
}

CurrentIncomeStatementCtrl.resolve = {
	response: function($q, $http) {
		return $http.get('/api/currentincomestatement.json');
	}
};


function PreviousIncomeStatementCtrl($scope, response) {
	$scope.balance = response.data;
}

PreviousIncomeStatementCtrl.resolve = {
	response: function($q, $http) {
		return $http.get('/api/previousincomestatement.json');
	}
};


function NetWorthCtrl($scope, response) {
	var parseDate = d3.time.format("%d-%b-%Y").parse;

	$scope.linechart = response.data;
	$scope.linechart.Data.forEach(function(d) {
		d.X = parseDate(d.X);
		d.Y = parseFloat(d.Y);
	});
}

NetWorthCtrl.resolve = {
	response: function($q, $http) {
		return $http.get('/api/networth.json');
	}
};


function NavCtrl($scope, $http) {
	$http.get('/api/nav.json').success(function(data) {
		$scope.reports = data;	
	});
}
