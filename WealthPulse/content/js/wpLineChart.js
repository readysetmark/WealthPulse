angular.module('wpLineChart', [])
	.directive('wpLineChart', function() {
		return {
			restrict: 'E',
			scope: {
				wpData: '=',
			},
			template: '<div></div>',
			replace: true,
			link: function(scope, element, attrs) {
				var margin = {top: 20, right: 20, bottom: 30, left: 55},
					width = 600 - margin.left - margin.right,
					height = 400 - margin.top - margin.bottom;

				var x = d3.time.scale()
					.range([0, width]);

				var y = d3.scale.linear()
					.range([height, 0]);

				var xAxis = d3.svg.axis()
					.scale(x)
					.ticks(d3.time.months, 3)
					.tickFormat(d3.time.format("%b %y"))
					.orient("bottom");

				var yAxis = d3.svg.axis()
					.scale(y)
					.orient("left");

				var line = d3.svg.line()
					.x(function(d) { return x(d.X); })
					.y(function(d) { return y(d.Y); });

				var svg = d3.select(element[0]).append("svg")
					.attr("width", width + margin.left + margin.right)
					.attr("height", height + margin.top + margin.bottom)
					.append("g")
					.attr("transform", "translate(" + margin.left + "," + margin.top + ")");

				scope.$watch('wpData', function(newData, oldData) {
					svg.selectAll('*').remove();

					if (!newData) {
						return;
					}

					x.domain(d3.extent(newData, function(d) { return d.X; }))
						.nice(d3.time.month);
					y.domain(d3.extent(newData, function(d) { return d.Y; }));

					svg.append("g")
						.attr("class", "x axis")
						.attr("transform", "translate(0," + height + ")")
						.call(xAxis);

					svg.append("g")
						.attr("class", "y axis")
						.call(yAxis)
						.append("text")
						.attr("transform", "rotate(-90)")
						.attr("y", 6)
						.attr("dy", ".71em")
						.style("text-anchor", "end")
						.text("Amount ($)");

					svg.append("path")
						.datum(newData)
						.attr("class", "line")
						.attr("d", line);

					var node = svg.append("g")
						.attr("class", "nodes")
						.selectAll("circle")
						.data(newData)
						.enter()
						.append("circle")
						.attr("class", "node")
						.attr("r", 2)
						.attr("cx", function(d) { return x(d.X); })
						.attr("cy", function(d) { return y(d.Y); });

					var hover = svg.append("g")
						.attr("class", "node-hover")
						.style("display", "none");

					var hover_rect = hover.append("rect")
						.attr("class", "node-hover-rect")
						.attr("rx", "5")
						.attr("ry", "5")
						.attr("width", "120")
						.attr("height", "30");

					var hover_text = hover.append("text")
						.attr("class", "node-hover-text")
						.attr("transform", "translate(10,20)");

					node
						.on("mouseover", function(d) {
							var text_width = 120;
							var lines = 1;

							hover_text.selectAll("tspan").remove();
							var name = hover_text.append("tspan")
								.attr("class", "node-hover-text-name")
								.attr("x", "0")
								.text(d.Hover);

							text_width = name.node().getComputedTextLength();

							hover_rect
								.attr("width", text_width + 20);

							var translate_x = x(d.X) + 10;
							if (translate_x + text_width + 20 > width) {
								translate_x = x(d.X) - 30 - text_width;
							} 

							var translate_y = y(d.Y) + 10;
							if (translate_y + 30 > height) {
								translate_y = y(d.Y) - 40;
							}

							hover
								.attr("transform", "translate(" + translate_x + ", " + translate_y +")")
								.style("display", "block");
						})
						.on("mouseout", function(d) {
							hover.style("display", "none");
						})
				});
			}
		};
	});
