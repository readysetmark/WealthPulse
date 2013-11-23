angular.module("wealthpulseApp", ['ngRoute'])
	.config(function($routeProvider) {
		$routeProvider
			.when('/balance', {controller:BalanceCtrl, templateUrl:'/content/balance.html'})
			.when('/networth', {controller:NetWorthCtrl, templateUrl:'/content/linechart.html'})
			.when('/currentincomestatement', {controller:CurrentIncomeStatementCtrl, templateUrl:'/content/balance.html'})
			.when('/previousincomestatement', {controller:PreviousIncomeStatementCtrl, templateUrl:'/content/balance.html'})
			.otherwise({redirectTo:'/balance'});
	});
	// .directive('linechart', function() {
	// 	return {
	// 		restrict: 'E',
	// 		transclude: false,
	// 		scope: {},

	// 	}
	// });

function BalanceCtrl($scope, $http) {
	$http.get('/api/balance.json').success(function (data) {
		$scope.balance = data;
		console.log($scope.reports);
	});
}

function CurrentIncomeStatementCtrl($scope, $http) {
	$http.get('/api/currentincomestatement.json').success(function (data) {
		$scope.balance = data;
	});
}

function PreviousIncomeStatementCtrl($scope, $http) {
	$http.get('/api/previousincomestatement.json').success(function (data) {
		$scope.balance = data;
	});
}

function NetWorthCtrl($scope, $http) {
	$http.get('/api/networth.json').success(function (data) {
		$scope.linechart = data;
	});
}

function NavCtrl($scope) {
	$scope.reports = [
		{url: "#/balance", title: "Balance"},
		{url: "#/networth", title: "Net Worth"},
		{url: "#/currentincomestatement", title: "Current Income Statement"},
		{url: "#/previousincomestatement", title: "Previous Income Statement"}
	];
}
