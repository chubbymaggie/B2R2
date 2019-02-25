/*
  B2R2 - the Next-Generation Reversing Platform

  Author: Sang Kil Cha <sangkilc@kaist.ac.kr>
          Soomin Kim <soomink@kaist.ac.kr>

  Copyright (c) SoftSec Lab. @ KAIST, since 2016

  Permission is hereby granted, free of charge, to any person obtaining a copy
  of this software and associated documentation files (the "Software"), to deal
  in the Software without restriction, including without limitation the rights
  to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
  copies of the Software, and to permit persons to whom the Software is
  furnished to do so, subject to the following conditions:

  The above copyright notice and this permission notice shall be included in all
  copies or substantial portions of the Software.

  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
  SOFTWARE.
*/

// Set the bottom margin of the entire UI
var bottomMargin = 30;

// Set the right margin of the entire UI
var rightMargin = 10;

// Set the ratio between the CFG VP and the minimap VP.
var minimapRatio = 0.4;

// Set the padding size for each CFG node.
var padding = 4;

// Node border thickness.
var nodeBorderThickness = 3;

// Edge thickness.
var edgeThickness = 3;

// The duration time for zooming when both nodes and edges are double clicked.
var focusMovementDuration = 750;

function initMarker(defs, id)
{
    defs.append("marker")
        .attr("id", id).attr("class", id)
        .attr("markerWidth", 3)
        .attr("markerHeight", 3)
        .attr("markerUnits", "strokeWidth")
        .attr("viewBox", "-5 -5 10 10")
        .attr("refX", 5)
        .attr("refY", 0)
        .attr("orient", "auto")
        .append("path")
        .attr("d", "M 0,0 m -5,-5 L 5,0 L -5,5 Z");
}

function initSVG()
{
    // Clear up the existing elements.
    $("#cfg").empty();
    $("#minimap").empty();

    // Create a top layer for drawing a CFG on the main window.
    d3.select("svg#cfg").append("g").attr("id", "cfgStage")
        .attr("transform", "scale (1)");

    // Create the main group for a CFG. This is to easily maintain the
    // coordinates of the main graph.
    d3.select("g#cfgStage").append("g").attr("id", "cfgGrp");

    // Several definitions to use to draw a CFG.
    let defs = d3.select("g#cfgGrp").append("defs");
    initMarker(defs, "cfgJmpEdgeArrow");
    initMarker(defs, "cfgCJmpTrueEdgeArrow");
    initMarker(defs, "cfgCJmpFalseEdgeArrow");

    // Add filters.
    defs.append("filter").attr("id", "cfgBlur")
        .append("feGaussianBlur").attr("stdDeviation", 2);

    // First draw an empty background for minimap.
    d3.select("svg#minimap")
        .append("rect").attr("width", "100%").attr("height", "100%")
        .attr("fill", "white");

    // Create a top layer for drawing a CFG on the minimap.
    d3.select("svg#minimap").append("g").attr("id", "minimapStage");
}

function copyToClipboard(str)
{
    let aux = document.createElement("textarea");
    aux.value = str;
    document.body.appendChild(aux);
    aux.select();
    document.execCommand("copy");
    document.body.removeChild(aux);
}

function initEvents(cfg)
{
    $(function(){
        $("#menuCopyCFG").click(function(e) {
            e.preventDefault();
            let mymodal = $("#codeCopyCFG");
            mymodal.text(JSON.stringify(cfg, null, " "));
        });
    })

    $(function(){
        $("#btnCopyCFG").click(function(e) {
            copyToClipboard($("#codeCopyCFG").text());
        });
    })
}

function appendDisasmFragment(txt, cls, fragment, isOpcode)
{
    let t = txt.append("tspan")
        .text(fragment).attr("class", cls).attr("xml:space", "preserve");

    if (isOpcode) t.attr("x", padding / 2).attr("dy", "14px");
    else t.attr("dx", "0px");
}

function strRepeat(str, num)
{
    if (num < 0) return "";
    else if (num === 1) return str;
    else return str + strRepeat(str, num - 1);
}

function drawNode(v)
{
    let g = d3.select("g#cfgGrp").append("g");

    let rect = g.append("rect")
        .attr("class", "cfgNode")
        .attr("fill", "white")
        .attr("stroke", "black")
        .attr("stroke-width", nodeBorderThickness);

    // Additional layer for bluring.
    let rectBlur = g.append("rect")
        .attr("class", "cfgNodeBlur")
        .attr("id", v.NodeID)
        .attr("fill", "transparent")
        .attr("stroke", "black")
        .attr("stroke-width", nodeBorderThickness);

    let text = g.append("text").attr("font-family", "'Inconsolata', monospace");

    let i, j;
    for (i = 0; i < v.Terms.length; i++) {
        let terms = v.Terms[i];
        let s = terms[0][0];
        let tag = terms[0][1];
        let mnemonic = s + strRepeat(" ", (s.length > 8 ? 0 : 8 - s.length));
        appendDisasmFragment(text, "cfgDisasmText " + tag, mnemonic, true);
        if (terms.length > 2) {
            for (j = 1; j < terms.length; j++) {
                let s = terms[j][0];
                let tag = terms[j][1];
                if (j == terms.length - 2) {
                    appendDisasmFragment(text, "cfgDisasmText " + tag, s, false);
                } else if (j == terms.length - 1) {
                    if (s.length > 0) {
                        let comment = " # " + s;
                        appendDisasmFragment(text, "cfgDisasmComment", comment, false);
                    }
                } else {
                    appendDisasmFragment(text, "cfgDisasmText " + tag, s, false);
                    appendDisasmFragment(text, "cfgDisasmText", ",", false);
                }
            }
        }
    }

    rect.attr("width", v.Width).attr("height", v.Height);
    rectBlur.attr("width", v.Width).attr("height", v.Height);

    g.attr("transform", "translate (" + v.Pos.X + "," + v.Pos.Y + ")");

    d3.select("g#minimapStage").append("rect")
        .attr("class", "minimapRects")
        .attr("rx", "1").attr("ry", "1")
        .attr("fill", "white").attr("fill-opacity", "0.25")
        .attr("style", "outline: 1px solid black;")
        .attr("width", v.Width * minimapRatio)
        .attr("height", v.Height * minimapRatio)
        .attr("transform",
              "translate(" + v.Pos.X * minimapRatio +
              ", " + v.Pos.Y * minimapRatio + ")");
}

function drawNodes(g)
{
    for (let i = 0; i < g.Nodes.length; i++) {
        drawNode(g.Nodes[i]);
    }

    let r   = document.getElementById("cfgGrp").getBBox(),
        obj = {
            width: r.width,
            height: r.height
        };

    return obj;
}

function drawEdge(e)
{
    let lineFunction = d3.line()
        .x(function (d) { return d.X; })
        .y(function (d) { return d.Y; });

    lineFunction.curve(d3.curveMonotoneY);

    // Additional line for bluring.
    d3.select("g#cfgGrp").insert("path", ":first-child")
        .attr("class", "cfg" + e.Type + "Blur" + " cfgEdgeBlur")
        .attr("d", lineFunction(e.Points))
        .attr("stroke", "transparent")
        .attr("stroke-width", edgeThickness)
        .attr("fill", "none");

    let p = d3.select("g#cfgGrp").insert("path", ":first-child")
        .attr("class", "cfg" + e.Type)
        .attr("d", lineFunction(e.Points))
        .attr("stroke-width", edgeThickness)
        .attr("fill", "none");

    if (e.IsBackEdge) p.attr("stroke-dasharray", "4, 4");

    p.attr("marker-end", "url(#cfg" + e.Type + "Arrow)");

    let miniLineFunction = d3.line()
        .x(function (d) { return d.X * minimapRatio; })
        .y(function (d) { return d.Y * minimapRatio; })
        .curve(d3.curveLinear);

    let m = d3.select("g#minimapStage").insert("path", ":first-child")
        .attr("d", miniLineFunction(e.Points))
        .attr("stroke", "black")
        .attr("stroke-width", 0.5)
        .attr("fill", "none");

    if (e.IsBackEdge) m.attr("stroke-dasharray", "2, 2");
}

function drawEdges(g)
{
    for (let i = 0; i < g.Edges.length; i++) {
        drawEdge(g.Edges[i]);
    }
}

function centerAlign(dims, shiftX, reductionRate)
{
    let leftPadding = (dims.cfgVPDim.width) / 2 / reductionRate;

    d3.select("g#cfgGrp").attr("transform",
                               "translate(" + leftPadding  + ", 0)");

    d3.select("g#minimapStage")
        .attr("transform",
              "translate (" + shiftX + ", 0) scale (" + reductionRate + ")");
}

function setMinimap(dims)
{
    let newWidth = dims.minimapVPDim.width;
    let newHeight = dims.minimapVPDim.height;

    // set minimap size based on the graph size.
    d3.select("svg#minimap")
        .attr("width", newWidth + "px").attr("height", newHeight + "px");

    // set size of the minimap nodes.
    let nodeSize = Math.ceil(Math.log(newWidth / 1000) / Math.log(2));
    if (nodeSize <= 0) nodeSize = 1;

    let nodes = d3.select("g#minimapStage").selectAll(".minimapRects");
    nodes.attr("style", "outline: " + nodeSize + "px" + " solid black;");

    return newWidth / 2;
}

function drawMinimapViewPort(dims)
{
    d3.select("svg#minimap")
        .append("rect")
        .attr("id", "minimapVP")
        .attr("width", (dims.minimapVPDim.width - 2) + "px")
        .attr("height", (dims.minimapVPDim.height - 2) + "px")
        .attr("fill", "transparent");
}

function drawCFG(dims, cfg)
{
    $("#icon-refresh").addClass("rotating"); // Start the animation.
    // This is to make sure that the rotation animation is running first.
    setTimeout(function () { drawCFGAux(dims, cfg); }, 5);
}

function drawCFGAux(dims, cfg)
{
    let extraRatio = 0.9, // Give a little bit more space.
        stageDim = null,
        shiftX = 0.0,
        reductionRate;

    initSVG();
    initEvents(cfg);

    stageDim = drawNodes(cfg);
    drawEdges(cfg);

    reductionRate =
        Math.min(dims.cfgVPDim.width / stageDim.width,
                 dims.cfgVPDim.height / stageDim.height) * extraRatio;

    // If the entire CFG is smaller than the cfgVP, then simply use the rate 1.
    // In other words, the maximum reductionRate is one.
    if (reductionRate >= 1) reductionRate = 1;

    shiftX = setMinimap(dims);
    centerAlign(dims, shiftX, reductionRate);
    drawMinimapViewPort(dims);
    registerEvents(reductionRate, dims, cfg);
    $("#icon-refresh").removeClass("rotating"); // Stop the animation.
}

function registerEvents(reductionRate, dims, g)
{
    let zoom = null;
    let translateWidthRatio = null;
    let translateHeightRatio = null;
    let offsetX = null;
    let offsetY = null;
    let inverseK = reductionRate;
    let transX = 0;
    let transY = 0;
    let transK = 1 / reductionRate;
    let minimapBound =
        document.getElementById("minimapStage").getBoundingClientRect();

    let cfg = d3.select("svg#cfg");
    let cfgStage = d3.select("g#cfgStage");
    let minimap = d3.select("svg#minimap");
    let minimapVP = d3.select("rect#minimapVP");
    let nodes = cfgStage.selectAll(".cfgNodeBlur");
    let edges = cfgStage.selectAll(".cfgEdgeBlur");
    let texts = cfgStage.selectAll(".cfgDisasmText");

    function getEdgePts(edge) {
        return edge.split(/M|L/)
                   .filter(function (el) { return el.length != 0; });
    }

    function convertvMapPtToVPCoordinate(dx, dy) {
        let miniVPBound =
            document.getElementById("minimapVP").getBoundingClientRect();

        let widthRatio = minimapRatio / translateWidthRatio;
        let halfWidth = miniVPBound.width / minimapRatio / 2;

        return { x: dx + halfWidth * widthRatio, y: dy };
    }

    function getPointFromEdgePts(edgePts, index) {
        let lastPts = edgePts[index].split(",");
        let lastX = parseFloat(lastPts[lastPts.length - 2]) * reductionRate;
        let lastY = parseFloat(lastPts[lastPts.length - 1]) * reductionRate;

        return { x: lastX, y: lastY };
    }

    function toCenter(dx, dy, accelerationRate) {
        let miniVPBound =
            document.getElementById("minimapVP").getBoundingClientRect();

        let widthRatio = minimapRatio / translateWidthRatio;
        let heightRatio = minimapRatio / translateHeightRatio;

        let halfWidth = miniVPBound.width / minimapRatio / 2;
        let halfHeight = miniVPBound.height / minimapRatio / 2;

        let newX = (halfWidth - dx) * widthRatio;
        let newY = (halfHeight - dy) * heightRatio;

        cfg.transition()
            .duration(focusMovementDuration * accelerationRate)
            .call(zoom.transform,
            d3.zoomIdentity.translate(newX, newY).scale(transK));
    }

    function getMousePos() {
        let mouse = d3.mouse(document.getElementById("cfgStage"));

        return { x: mouse[0] * reductionRate, y: mouse[1] * reductionRate }
    }

    // Returns an integer from 1.0 to 2.0 depending on the length.
    function getAccelerationRate(lastPt, mousePt) {
        let xpow = Math.pow(lastPt.x - mousePt.x, 2);
        let ypow = Math.pow(lastPt.y - mousePt.y, 2);
        let currentLength = Math.sqrt(xpow + ypow);
        let r = currentLength / (100 + currentLength) + 1.0;
        return r;
    }

    edges.each(function (d, i) {
        d3.select(this).on("dblclick", function () {
            let edge = d3.select(this).attr("d");
            let edgePts = getEdgePts(edge);
            let lastPt = getPointFromEdgePts(edgePts, edgePts.length - 1);
            let vMapLastPt = convertvMapPtToVPCoordinate(lastPt.x, lastPt.y);
            let mousePt = getMousePos();

            let acceleration = getAccelerationRate(vMapLastPt, mousePt);

            // As b2r2.css has .glyphicon { padding-right: 5px; }, which is used
            // when dims are generated at reloadUI(), 5px plus to vMapPt.x
            // has to be considered as long as the padding has been maintained.
            toCenter(vMapLastPt.x + 5, vMapLastPt.y, acceleration);
        });
    });

    nodes.each(function (d, i) {
        d3.select(this).on("click", function () {
            let rect = $(".cfgNode")[i];
            if (rect.classList.contains("nodeHighlight")) {
                d3.select(rect).classed("nodeHighlight", false);
            } else {
                $(".cfgNode").removeClass("nodeHighlight");
                d3.select(rect).classed("nodeHighlight", true);
            }
        });
    });

    nodes.each(function (d, i) {
        d3.select(this).on("dblclick", function () {
            let id = d3.select(this).attr("id");
            let x = g.Nodes[Number(id)].Pos.X * reductionRate;
            let y = g.Nodes[Number(id)].Pos.Y * reductionRate;
            let vMapPt = convertvMapPtToVPCoordinate(x, y);
            let halfHeight = d3.select(this).attr("height") / 2 * reductionRate;

            toCenter(vMapPt.x + 5, vMapPt.y + halfHeight, 100);
        });

    });

    texts.on("click", function () {
        // Remove all highlights for cfgDisasmText
        $(".cfgDisasmText").removeClass("wordHighlight");
        let clsName = d3.select(this).attr("class").split(" ")[1];
        // Only highlights with "clsName"
        // XXX: later we should change false to true
        texts.filter("." + clsName).classed("wordHighlight", false);
    });

    function getEventPointFromMinimap(event) {
        let svgSource = document.getElementById("minimap");
        let viewerPoint = svgSource.createSVGPoint();

        viewerPoint.x = event.clientX;
        viewerPoint.y = event.clientY;

        return viewerPoint.matrixTransform(svgSource.getScreenCTM().inverse());
    }

    function jumpToCursor() {
        let centerPoint = getEventPointFromMinimap(d3.event.sourceEvent);
        let minimapX = centerPoint.x - offsetX;
        let minimapY = centerPoint.y - offsetY;
        let minimapK = reductionRate * inverseK;

        offsetX = (dims.minimapVPDim.width * minimapK) / 2;
        offsetY = (dims.minimapVPDim.height * minimapK) / 2;
        minimapVP.attr("transform",
                       "translate(" + minimapX + ","
                                    + minimapY + ") scale(" + minimapK + ")");

        transX = - minimapX / translateWidthRatio;
        transY = - minimapY / translateHeightRatio;
        cfgStage.attr("transform",
                      "translate(" + transX + ","
                                   + transY + ") scale(" + transK + ")");

        zoom.transform(cfg,
                       d3.zoomIdentity.translate(transX, transY).scale(transK));
    }

    function dragStart() {
        let evt = d3.event.sourceEvent;
        let vp = document.getElementById("minimapVP").getBoundingClientRect();

        offsetX = evt.clientX - vp.left;
        offsetY = evt.clientY - vp.top;
    }

    function dragMove() {
        let mouseSVGPoint = getEventPointFromMinimap(d3.event.sourceEvent);
        let minimapX = mouseSVGPoint.x - offsetX;
        let minimapY = mouseSVGPoint.y - offsetY;
        let minimapK = reductionRate * inverseK;

        transX = - minimapX / translateWidthRatio;
        transY = - minimapY / translateHeightRatio;

        minimapVP.attr("transform",
                       "translate(" + minimapX + ","
                                    + minimapY + ") scale(" + minimapK + ")");
        cfgStage.attr("transform",
                      "translate(" + transX + ","
                                   + transY + ") scale(" + transK + ")");
        zoom.transform(cfg,
                       d3.zoomIdentity.translate(transX, transY).scale(transK));
    }

    function dragEnd() {
        document.getElementById("minimap").style.cursor = "default";
    }

    let dragBehavior = d3.drag()
        .on("start", dragStart)
        .on("drag", dragMove)
        .on("end", dragEnd);

    minimapVP.call(dragBehavior);

    let clickAndDrag = d3.drag()
        .on("start", jumpToCursor)
        .on("drag", dragMove)
        .on("end", dragEnd);

    minimap.call(clickAndDrag);

    document.getElementById("cfg")
        .addEventListener("mousemove", (function () { /*empty*/ }).bind(this));

    function zoomed() {
        cfgStage.attr("transform", d3.event.transform);

        transX = d3.event.transform.x;
        transY = d3.event.transform.y;
        transK = d3.event.transform.k;

        inverseK = 1 / transK;
        let minimapK = reductionRate * inverseK;

        let viewportBound =
            document.getElementById("cfgStage").getBoundingClientRect();

        translateWidthRatio = minimapBound.width / viewportBound.width;
        translateHeightRatio = minimapBound.height / viewportBound.height;

        transX = (- transX) * translateWidthRatio;
        transY = (- transY) * translateHeightRatio;

        minimapVP.attr("transform",
                       "translate(" + transX + ","
                                    + transY + ") scale(" + minimapK + ")")
             .attr("style", "stroke-width:" + (1.5 / (minimapK)) + "px");
    }

    zoom = d3.zoom().scaleExtent([reductionRate, 20]).on("zoom", zoomed);
    let transform = d3.zoomIdentity.translate(0, 0).scale(reductionRate);
    cfg.call(zoom).call(zoom.transform, transform).on("dblclick.zoom", null);
}

function drawFunctions(funcs)
{
    $.each(funcs, function (_, addr) {
        $("#funcSelector").append($('<option>', {
            value: addr,
            text: addr
        }));
    });
}

function drawBinInfo(str)
{
    $("#binInfo").text(function(_, _) { return str; });
}

function query(name, arguments, callback)
{
    let req = new XMLHttpRequest();
    let q = encodeURIComponent(name);
    let args = encodeURIComponent(arguments);
    req.onreadystatechange = function () {
        if (this.readyState == 4 && this.status == 200) {
            callback(JSON.parse(this.responseText));
        }
    }
    req.open("GET", "/ajax/?q=" + q + "&args=" + args, true);
    req.send();
}

function registerRefreshEvents(dims, json)
{
    $("#btn-refresh").click(function () {
        drawCFG(dims, json);
    });
}

// Offline mode renders a single function only. Inter-function analysis is not
// supported in offline mode.
function runOffline(dims)
{
    fileInput = document.getElementById("cfgFile");
    $("#uiTitle").click(function () { fileInput.click(); });
    fileInput.addEventListener("change", function () {
        let file = fileInput.files[0];
        let reader = new FileReader();
        reader.onload = function () {
            let json = JSON.parse(reader.result);
            drawCFG(dims, json);
            registerRefreshEvents(dims, json);
        };
        try { reader.readAsText(file); }
        catch (_) { console.log("Error: File open failure."); }
    });
}

function isEmpty(obj) {
    for (var key in obj) {
        if (obj.hasOwnProperty(key))
            return false;
    }
    return true;
}

// Run in online mode (this is the default).
function runOnline(dims)
{
    $("#funcSelector").change(function () {
        query("cfg",
              $("#funcSelector option:selected").text(),
              function (json) {
                  if (!isEmpty(json)) {
                      $("#uiFuncName").text(function (_, _) {
                          return $("#funcSelector option:selected").text();
                      });
                      drawCFG(dims, json);
                      registerRefreshEvents(dims, json);
                  }
              });
    });
    query("functions", "", drawFunctions);
    query("bininfo", "", drawBinInfo);
}

function reloadUI()
{
    let minimapMarginRight =
        parseInt($("#cfgDiv").css("padding-right"))
        + parseInt($("#cfgDiv").css("margin-right"));

    let cfgVPDim = {
        width: document.getElementById("cfgDiv").getBoundingClientRect().width
            - parseInt($("#cfgDiv").css("padding-right"))
            - rightMargin,
        height: $(window).height() - bottomMargin
            - document.getElementById("uiTitle").getBoundingClientRect().height
            - document.getElementById("uiHeader").getBoundingClientRect().height
    };

    let minimapVPDim = {
        width: cfgVPDim.width * minimapRatio,
        height: cfgVPDim.height * minimapRatio
    };

    $("#minimapDiv").css("margin-right", minimapMarginRight + "px");
    $("#minimapDiv").css("margin-bottom", bottomMargin + "px");

    d3.select("svg#cfg")
        .attr("width", cfgVPDim.width)
        .attr("height", cfgVPDim.height);

    d3.select("svg#minimap")
        .attr("width", minimapVPDim.width)
        .attr("height", minimapVPDim.height);

    $("#funcSelector")
        .attr("style", "height: " + cfgVPDim.height + "px");

    return { cfgVPDim: cfgVPDim, minimapVPDim: minimapVPDim };
}

function main()
{
    let dims = reloadUI();
    $(window).resize(function() { reloadUI(); });

    if (window.location.protocol == "file:")
        return runOffline(dims);
    else
        return runOnline(dims);
}

if (typeof window === 'undefined') { // For Node.js
    module.exports.initSVG = initSVG;
    module.exports.initEvents = initEvents;
} else {
    window.addEventListener('load', function() { main(); }, false);
}
