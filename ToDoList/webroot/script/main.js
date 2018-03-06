var app = angular.module("todoApp", []);
app.controller("mainController", function($scope, $timeout, $http) {

  var config = {
    xsrfHeaderName: "X-XSRF-Token",
    xsrfCookieName: "xsrf-token"
  };

  $scope.items = [
    {"completed": true, "text": "My item"},
    {"completed": false, "text": "Working hard"},
    {"completed": false, "text": "More todo"}
  ];

  $scope.hasLoaded = false;
  $scope.dirtydata = false;
  $scope.serverItems = $scope.items;

  $scope.addItem = function() {
    if (($scope.newtext || "").trim().length == 0) {
      return;
    }

    $scope.items.push({"completed": false, "text": $scope.newtext});
    $scope.newtext = null;
  };

  $scope.allAsDone = function() {
    for (var i = $scope.items.length - 1; i >= 0; i--) {
      $scope.items[i].completed = true;
    }
  };

  $scope.remove = function(item) {
    for (var i = $scope.items.length - 1; i >= 0; i--) {
      if ($scope.items[i] == item) {
        $scope.items.splice(i, 1);
      }
    }
  };

  $scope.removeAllCompleted = function() {
    for (var i = $scope.items.length - 1; i >= 0; i--) {
      if ($scope.items[i].completed) {
        $scope.items.splice(i, 1);
      }
    }
  };

  var saveActive = false;
  var savePending = false;
  $scope.saveData = function() {
    if (!$scope.hasLoaded) {
       return;
    }

    if (angular.equals($scope.items, $scope.serverItems)) {
      return;
    }

    if (saveActive) {
      savePending = true;
      return;
    }

    saveActive = true;
    savePending = false;
    var saveitems = angular.copy($scope.items);

    $http.put(
      "/api/v1/todolist",
      $scope.items,
      config
    ).then(
      function(response) {
        saveActive = false;
        $scope.dirtydata = false;
        $scope.serverItems = saveitems;
        $scope.failuretext = null;

        if (savePending)
          $scope.saveData();        
      },

      function(response) {
        saveActive = false;
        var text = (response.statusText || "").trim().length == 0 ? "Connection error" : response.statusText;
        $scope.failuretext = "Save failed with: " + response.status + " " + text;

        if (savePending)
          $scope.saveData();
        else
          $timeout(function() { $scope.saveData(); }, 1000);
      }
    );
  };

  var updateTimer = null;

  $scope.$watch("items", function() {

    var remains = 0;
    for (var i = $scope.items.length - 1; i >= 0; i--) {
      if (!$scope.items[i].completed) {
        remains++;
      }
    }

    $scope.itemsleft = remains;

    if (!$scope.hasLoaded) {
      return;
    }

    if (angular.equals($scope.items, $scope.serverItems)) {
      return;
    }

    $scope.dirtydata = true;

    if (updateTimer != null) {
      $timeout.cancel(updateTimer);
    }

    updateTimer = $timeout(function() { updateTimer = null; $scope.saveData(); }, 1000);

  }, true);

  $scope.dirtydata = false;

  $scope.loadData = function() {
    $http.get(
      "/api/v1/todolist",
      config
    ).then(
      function(response) {
        $scope.items = response.data;
        $scope.serverItems = angular.copy(response.data);

        $scope.dirtydata = false;
        $scope.hasLoaded = true;

        $scope.failuretext = null;
      },

      function(response) {
        var text = (response.statusText || "").trim().length == 0 ? "Connection error" : response.statusText;
        $scope.failuretext = "Load failed with: " + response.status + " " + text;

        $timeout(function() { $scope.loadData(); }, 1000);
      }
    );
  };

  $scope.loadData();

});