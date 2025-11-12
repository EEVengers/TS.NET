class ResultMetadataXYChart {
    constructor(containerId, options = {}) {
        this.containerId = containerId;
        this.svg = d3.select(`#${containerId}`);
        
        this.options = {
            width: options.width || 700,
            height: options.height || 460,
            margin: options.margin || { top: 40, right: 20, bottom: 60, left: 80 },
            showGrid: options.showGrid !== undefined ? options.showGrid : true,
            showLegend: options.showLegend !== undefined ? options.showLegend : true
        };
        
        this.chartData = null;
        this.xScale = null;
        this.yScale = null;
    }
    
    render(data) {
        this.chartData = data;
        
        if (!this.validateData(data)) {
            console.error('Invalid chart data format');
            return;
        }
        
        this.svg.selectAll('*').remove();
        
        const margin = this.options.margin;
        const width = this.options.width - margin.left - margin.right;
        const height = this.options.height - margin.top - margin.bottom;
        
        // Add border
        this.svg.append('rect')
            .attr('x', 0)
            .attr('y', 0)
            .attr('width', this.options.width)
            .attr('height', this.options.height)
            .attr('fill', 'white')
            .attr('stroke', '#bbb')
            .attr('stroke-width', 1);
        
        const g = this.svg.append('g')
            .attr('transform', `translate(${margin.left},${margin.top})`);
        
        this.createScales(data, width, height);
        
        // Add clipping path
        this.svg.append('defs').append('clipPath')
            .attr('id', `clip-${this.containerId}`)
            .append('rect')
            .attr('x', 0)
            .attr('y', 0)
            .attr('width', width)
            .attr('height', height);
        
        if (this.options.showGrid) {
            this.createGrid(g, width, height);
        }
        
        this.addAxes(g, width, height);
        this.addSeries(g);
        this.addTitle();
        this.addAxisLabels(width, height);
        
        if (this.options.showLegend) {
            this.addLegend(width);
        }
    }
    
    validateData(data) {
        return data && data.xAxis && data.yAxis && data.series && Array.isArray(data.series) && data.series.length > 0;
    }
    
    createScales(data, width, height) {
        const xExtent = this.getDataExtent('x');
        const yExtent = this.getDataExtent('y');
        
        if (data.xAxis.scale.toLowerCase() === 'log10') {
            this.xScale = d3.scaleLog()
                .domain([Math.max(xExtent[0], 1e-10), xExtent[1]])
                .range([0, width]);
        } else {
            this.xScale = d3.scaleLinear()
                .domain(xExtent)
                .range([0, width])
                .nice();
        }
        
        if (data.yAxis.scale.toLowerCase() === 'log10') {
            this.yScale = d3.scaleLog()
                .domain([Math.max(yExtent[0], 1e-10), yExtent[1]])
                .range([height, 0]);
        } else {
            this.yScale = d3.scaleLinear()
                .domain(yExtent)
                .range([height, 0])
                .nice();
        }
    }
    
    getDataExtent(axis) {        
        var allValues = this.chartData.series.flatMap(series => 
            series.data.map(d => d[axis])
        );
        if(this.chartData[axis + "Axis"].additionalRangeValues != null)
            allValues = allValues.concat(this.chartData[axis + "Axis"].additionalRangeValues);
        if(this.chartData[axis + "Axis"].scale.toLowerCase() === 'log10')
            return [Math.pow(10, Math.floor(Math.log10(d3.min(allValues)))), Math.pow(10, Math.ceil(Math.log10(d3.max(allValues))))];
        else
            return [d3.min(allValues), d3.max(allValues)];
    }
    
    createGrid(g, width, height) {
        const xIsLog = this.chartData.xAxis.scale.toLowerCase() === 'log10';
        const yIsLog = this.chartData.yAxis.scale.toLowerCase() === 'log10';
        
        // Minor grid
        if (xIsLog) {
            const xExtent = this.xScale.domain();
            const xMajorTicks = this.decadeTicks(xExtent[0], xExtent[1]);
            const xMinorTicks = this.generateMinorTicks(xExtent);
            
            // Minor grid
            g.append('g')
                .attr('class', 'grid-x-minor')
                .attr('transform', `translate(0, ${height})`)
                .call(d3.axisBottom(this.xScale)
                    .tickValues(xMinorTicks)
                    .tickSize(-height)
                    .tickFormat(""))
                .selectAll('line')
                .style('stroke', '#eee');

            // Major grid
            g.append('g')
            .attr('class', 'grid-x-major')
            .attr('transform', `translate(0, ${height})`)
            .call(d3.axisBottom(this.xScale)
                .tickValues(xMajorTicks)
                .tickSize(-height)
                .tickFormat(''))
            .selectAll('line')
            .style('stroke', '#bbb');
        }
        else {
            // Major grid
            g.append('g')
            .attr('class', 'grid-x-major')
            .attr('transform', `translate(0, ${height})`)
            .call(d3.axisBottom(this.xScale)
                .ticks(10)
                .tickSize(-height)
                .tickFormat(''))
            .selectAll('line')
            .style('stroke', '#bbb');
        }
        
        if (yIsLog) {
            const yExtent = this.yScale.domain();
            const yMinorTicks = this.generateMinorTicks(yExtent);
            
            g.append('g')
                .attr('class', 'grid-y-minor')
                .call(d3.axisLeft(this.yScale)
                    .tickValues(yMinorTicks)
                    .tickSize(-width)
                    .tickFormat(""))
                .selectAll('line')
                .style('stroke', '#eee');
        }
                
        g.append('g')
            .attr('class', 'grid-y-major')
            .call(d3.axisLeft(this.yScale)
                .tickSize(-width)
                .tickFormat(''))
            .selectAll('line')
            .style('stroke', '#bbb');
        
        // g.selectAll('.grid-major path, .grid-minor path').style('stroke-width', 0);
    }
    
    generateMinorTicks(extent) {
        const minorTicks = [];
        const startDecade = Math.floor(Math.log10(extent[0]));
        const endDecade = Math.ceil(Math.log10(extent[1]));
        
        for (let decade = startDecade; decade <= endDecade; decade++) {
            const base = Math.pow(10, decade);
            for (let i = 2; i <= 9; i++) {
                const tick = base * i;
                if (tick >= extent[0] && tick <= extent[1]) {
                    minorTicks.push(tick);
                }
            }
        }
        return minorTicks;
    }
    
    decadeTicks(min, max) {
        // Get major ticks on log10 decades
        const ticks = [];
        const start = Math.ceil(Math.log10(min));
        const end = Math.floor(Math.log10(max));
        for (let i = start; i <= end; i++) {
        ticks.push(Math.pow(10, i));
        }
        return ticks;
    }

    addAxes(g, width, height) {     
        const xIsLog = this.chartData.xAxis.scale.toLowerCase() === 'log10';
        const yIsLog = this.chartData.yAxis.scale.toLowerCase() === 'log10';

        var xAxis = d3.axisBottom(this.xScale)
            .ticks(10)
            .tickFormat(d3.format('~s'));

        if(xIsLog)
        {
            const xExtent = this.xScale.domain();
            const xMajorTicks = this.decadeTicks(xExtent[0], xExtent[1]);
            xAxis = d3.axisBottom(this.xScale)
                .tickValues(xMajorTicks)
                .tickFormat(d3.format('~s'));
        }
        
        const yAxis = d3.axisLeft(this.yScale)
            .ticks(10)
            .tickFormat(d3.format('~s'));
        
        g.append('g')
            .attr('class', 'x axis')
            .attr('transform', `translate(0, ${height})`)
            .call(xAxis)
            .selectAll('path, line')
            .style('fill', 'none')
            .style('stroke', '#555');
        
        g.append('g')
            .attr('class', 'y axis')
            .call(yAxis)
            .selectAll('path, line')
            .style('fill', 'none')
            .style('stroke', '#555');
        
        g.selectAll('.axis .tick text')
            .attr('class', 'text-xs');
    }
    
    addAxisLabels(width, height) {
        const margin = this.options.margin;
        
        this.svg.append('text')
            .attr('class', 'text-sm')
            .attr('x', margin.left + width / 2)
            .attr('y', margin.top + height + margin.bottom / 2 + 12)
            .style('text-anchor', 'middle')
            .style('alignment-baseline', 'central')
            .text(this.chartData.xAxis.label || 'X Axis');
        
        this.svg.append('text')
            .attr('class', 'text-sm')
            .attr('transform', `rotate(-90)`)
            .attr('x', -(margin.top + height / 2))
            .attr('y', margin.left / 2 - 18)
            .style('text-anchor', 'middle')
            .style('alignment-baseline', 'middle')
            .text(this.chartData.yAxis.label || 'Y Axis');
    }
    
    addSeries(g) {
        const clippedGroup = g.append('g')
            .attr('clip-path', `url(#clip-${this.containerId})`);
        
        const lineGenerator = d3.line()
            .x(d => this.xScale(d.x))
            .y(d => this.yScale(d.y));
        
        this.chartData.series.forEach((series, index) => {
            const color = series.colourHex || '#000';
            
            clippedGroup.append('path')
                .datum(series.data)
                .attr('class', `line series-${index}`)
                .attr('fill', 'none')
                .attr('stroke', color)
                .attr('stroke-width', 1)
                .attr('d', lineGenerator);
        });
    }
    
    addTitle() {
        if (this.chartData.title) {
            const margin = this.options.margin;
            this.svg.append('text')
                .attr('class', 'text-sm')
                .attr('x', margin.left + (this.options.width - margin.left - margin.right) / 2)
                .attr('y', margin.top / 2 + 8)
                .style('text-anchor', 'middle')
                .text(this.chartData.title);
        }
    }
    
    addLegend(width) {
        const margin = this.options.margin;
        const legendMargin = 5;
        const legendLineLength = 20;
        const legendEntryHeight = 24;

        var maxTextLength = Math.max(...this.chartData.series.map(s => s.name.length));
        var legendWidth = 6 + legendLineLength + 6 + maxTextLength * 7 + 6;   // Approximate width, updated later with real width

        const legend = this.svg.append('g')
            .attr('class', 'legend')
            .attr('transform', `translate(${margin.left + width - legendWidth - legendMargin}, ${margin.top + legendMargin})`);
        
        const legendBox = legend.append('rect')
            .attr('width', legendWidth)
            .attr('height', this.chartData.series.length * legendEntryHeight)
            .attr('fill', 'white')
            .attr('stroke', '#bbb');
        
        this.chartData.series.forEach((series, index) => {
            const colour = series.colourHex || '#000';
            const yOffset = index * legendEntryHeight + 12;
            
            legend.append('line')
                .attr('x1', 6)
                .attr('y1', yOffset)
                .attr('x2', 6 + legendLineLength)
                .attr('y2', yOffset)
                .attr('stroke', colour)
                .attr('stroke-width', 4);
            
            const legendText = legend.append('text')
                .attr('x', 6 + legendLineLength + 6)
                .attr('y', yOffset)
                .attr('alignment-baseline', 'central')
                .attr('class', 'text-xs')
                .text(series.name);

            const textElement = legendText.node();
            const textLength = textElement.getComputedTextLength();

            if(textLength > maxTextLength)
                maxTextLength = textLength;
        });

        legendWidth = 6 + legendLineLength + 6 + maxTextLength + 6;
        
        legendBox.attr('width', legendWidth);
        legend.attr('transform', `translate(${margin.left + width - legendWidth - legendMargin}, ${margin.top + legendMargin})`);
    }
    
    update(data) {
        this.render(data);
    }
    
    destroy() {
        this.svg.selectAll('*').remove();
        this.chartData = null;
    }
}

if (typeof module !== 'undefined' && module.exports) {
    module.exports = ResultMetadataXYChart;
}
