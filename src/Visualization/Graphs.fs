(*
  B2R2 - the Next-Generation Reversing Platform

  Author: Soomin Kim <soomink@kaist.ac.kr>

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
*)

namespace B2R2.Visualization

open B2R2
open B2R2.FrontEnd
open B2R2.BinGraph

open Microsoft.FSharpLu.Json

#if DEBUG
module Dbg =
  open System
  let private fs = IO.File.Create ("visualization.log")
  let private getBytes (s: string) =
    Text.Encoding.ASCII.GetBytes(s + Environment.NewLine)
  let logn s =
    let bytes = getBytes s
    fs.Write (bytes, 0, bytes.Length)
    fs.Flush ()
#endif

type DisasmData = {
  Disasm  : string
  Comment : string
}

// TODO: Should be fixed later
type Tag =
  | Mnemonic
  | Operand0
  | Operand1
  | Operand2
  | Comment

type Term = string * Tag

type InputNode = {
  Address     : Addr
  Disassembly : DisasmData list
}

type InputEdge = {
  From       : Addr
  To         : Addr
  Type       : CFGEdge
}

/// This is Visualization module's input type.
type InputGraph = {
  Nodes: InputNode list
  Edges: InputEdge list
  Root: Addr
}

module internal InputGraph =
  let ofFile s =
    match Compact.tryDeserializeFile<InputGraph> s with
    | Choice1Of2 c -> c
    | Choice2Of2 e -> failwith e

  let ofInstruction hdl (instr: Instruction) =
    { Disasm = instr.Disasm (true, true, hdl.FileInfo)
      Comment = "" }

  let ofDisassemblyBBL hdl iNodes (v: DisasmVertex) =
    let vData = v.VData
    let disasmData = List.map (ofInstruction hdl) vData.Instrs
    { Address = vData.AddrRange.Min ; Disassembly = disasmData } :: iNodes

  let ofCFGEdge (g: DisasmCFG) iEdges (src: DisasmVertex) (dst: DisasmVertex) =
    let edge = g.FindEdge src dst
    { From = src.VData.AddrRange.Min ; To = dst.VData.AddrRange.Min ;
      Type = edge } :: iEdges

  let ofDisasmCFG hdl (g: DisasmCFG) =
    let iNodes = g.FoldVertex (ofDisassemblyBBL hdl) []
    let iEdges = g.FoldEdge (ofCFGEdge g) []
    let root = g.GetRoot ()
    { Nodes = iNodes ; Edges = iEdges ; Root = root.VData.AddrRange.Min }

type Point = {
  X : float
  Y : float
}

type OutputNode = {
  Address     : Addr
  Terms       : Term list list
  Width       : float
  Height      : float
  Pos         : Point
}

type OutputEdge = {
  Type       : CFGEdge
  Points     : Point list
  IsBackEdge : bool
}

/// This is Visualization module's final output type.
type OutputGraph = {
  Nodes : OutputNode list
  Edges : OutputEdge list
}

module OutputGraph =
  let toFile s g = Compact.serializeToFile<OutputGraph> s g

  let toStr g = Compact.serialize<OutputGraph> g

type VNode (id, addr, terms, width, height, isDummy) =
  inherit VertexData (id)

  let mutable layer = -1
  let mutable pos = -1
  let mutable xPos = 0.0
  let mutable yPos = 0.0

  member __.Addr = addr

  member __.Terms = terms

  member __.Width = width

  member __.Height = height

  // This is different from that of Vertex<'V> type of BinGraph
  member __.IsDummy = isDummy

  member __.Layer with get() = layer and set(v) = layer <- v

  member __.Pos with get() = pos and set(v) = pos <- v

  member __.XPos with get() = xPos and set(v) = xPos <- v

  member __.YPos with get() = yPos and set(v) = yPos <- v

type VEdge (src, dst, ty) =
  let mutable isBackEdge = false
  let mutable points: (float * float) list = []

  member __.From = src

  member __.To = dst

  member __.Type = ty

  member __.IsBackEdge with get() = isBackEdge and set(v) = isBackEdge <- v

  member __.Points with get() = points and set(v) = points <- v

type VGraph = SimpleDiGraph<VNode, VEdge>

module VGraph =
  [<Literal>]
  let padding = 4.0

  let private calcLength { Disasm = disasm ; Comment = comment } =
    String.length disasm + String.length comment

  let private calcWidth disassembly =
    let maxLength = List.map calcLength disassembly |> List.max |> float
    // This number (7.5) is empirically obtained with the current font. For some
    // reasons, we cannot precisely determine the width of each text even though
    // we are using fixed-width fonts.
    // padding was added once more in the drawNode function
    maxLength * 7.5 + padding * 2.0

  let private calcHeight disassembly =
    let length = List.length disassembly |> float
    let tSpanOffset = 4.0
    // This number (14) is empirically obtained with the current font. The same
    // way as in getWidth function.
    // padding was added once more in the drawNode function
    length * 14.0 + tSpanOffset + padding * 2.0

  let disasmDataToTerms { Disasm = (disasm: string) ; Comment = comment } =
    let comment = comment, Comment
    let frags = disasm.Split ' ' |> Array.filter (fun x -> String.length x <> 0)
    if Array.length frags = 1 then
      let mnemonic = frags.[0], Mnemonic
      [ mnemonic ; comment ]
    else
      let mnemonic = frags.[0], Mnemonic
      let operands = String.concat " " frags.[1 .. ]
      let operands = operands.Split ','
      if Array.length operands = 1 then
        [ mnemonic ; (operands.[0], Operand0) ; comment ]
      elif Array.length operands = 2 then
        [ mnemonic
          (operands.[0], Operand0)
          (operands.[1], Operand1)
          comment ]
      elif Array.length operands = 3 then
        [ mnemonic
          (operands.[0], Operand0)
          (operands.[1], Operand1)
          (operands.[2], Operand2)
          comment ]
      else
        []

  let private addVNode (vGraph: VGraph) root vMap (iNode: InputNode) =
    let width = calcWidth iNode.Disassembly
    let height = calcHeight iNode.Disassembly
    let terms = List.map disasmDataToTerms iNode.Disassembly
    let vNode =
      VNode (vGraph.GenID (), iNode.Address, terms, width, height, false)
    let v = vGraph.AddVertex vNode
    if root = iNode.Address then vGraph.SetRoot v else ()
    Map.add iNode.Address v vMap

  let private addVEdge (vGraph: VGraph) vMap (iEdge: InputEdge) =
    let src = Map.find iEdge.From vMap
    let dst = Map.find iEdge.To vMap
    let vEdge = VEdge (iEdge.From, iEdge.To, iEdge.Type)
    vGraph.AddEdge src dst vEdge

  let ofIGraph { Nodes = iNodes ; Edges = iEdges ; Root = root } =
    let vGraph = VGraph ()
    let vMap = List.fold (addVNode vGraph root) Map.empty iNodes
    List.iter (addVEdge vGraph vMap) iEdges
    vGraph

  let toOutputNode oNodes (v: Vertex<VNode>) =
    let vData = v.VData
    let pos = { X = vData.XPos ; Y = vData.YPos }
    let oNode =
      { Address = vData.Addr ; Terms = vData.Terms ;
        Width = vData.Width ; Height = vData.Height ; Pos = pos }
    oNode :: oNodes

  let toOutputEdge (vGraph: VGraph) oEdges src dst =
    let eData = vGraph.FindEdge src dst
    let points = List.map (fun (x, y) -> { X = x ; Y = y }) eData.Points
    let oEdge =
      { Type = eData.Type ; Points = points ;
        IsBackEdge = eData.IsBackEdge }
    oEdge :: oEdges

  let toOutputGraph (vGraph: VGraph) =
    let oNodes = vGraph.FoldVertex toOutputNode []
    let oEdges = vGraph.FoldEdge (toOutputEdge vGraph) []
    { Nodes = oNodes ; Edges = oEdges }

#if DEBUG
  let private ppNode (vNode: Vertex<VNode>) =
    Dbg.logn "Node {"
    sprintf "\tID: %d" (vNode.GetID ()) |> Dbg.logn
    sprintf "\tAddr: %x" vNode.VData.Addr |> Dbg.logn
    sprintf "\tLayer: %d" vNode.VData.Layer |> Dbg.logn
    Dbg.logn "\tPreds: ["
    List.iter (fun (v: Vertex<VNode>) ->
      sprintf "%d, " (v.GetID ()) |> Dbg.logn) vNode.Preds
    Dbg.logn "]"
    Dbg.logn "\tSuccss: ["
    List.iter (fun (v: Vertex<VNode>) ->
      sprintf "%d, " (v.GetID ()) |> Dbg.logn) vNode.Succs
    Dbg.logn "]"
    Dbg.logn "}"
#endif

  let rec private checkStack visited (stack: Vertex<_> list) orderMap cnt =
    match stack with
    | [] -> (stack, orderMap, cnt)
    | v :: stack ->
      if List.exists (fun s -> Set.contains s visited |> not) v.Succs then
        (v :: stack, orderMap, cnt)
      else
        let orderMap = Map.add v cnt orderMap
        checkStack visited stack orderMap (cnt - 1)

  let private dfsOrdering (visited, stack, orderMap, cnt) v =
    let visited = Set.add v visited
    let stack, orderMap, cnt = checkStack visited (v :: stack) orderMap cnt
    visited, stack, orderMap, cnt

  let getDFSOrder (vGraph: VGraph) =
    let size = vGraph.Size () - 1
    let _, _, dfsOrder, _ =
      vGraph.FoldVertexDFS dfsOrdering (Set.empty, [], Map.empty, size)
    dfsOrder

#if DEBUG
  let pp (vGraph: VGraph) = vGraph.IterVertexDFS ppNode
#endif

  // Getters for graph
  let getSize (vGraph: VGraph) = vGraph.Size ()

  // Getters for fields in vertex
  let getID v = Vertex<VNode>.GetID v

  let getPreds (v: Vertex<_>) = v.Preds

  let getSuccs (v: Vertex<_>) = v.Succs

  let getAddr (v: Vertex<VNode>) =
    v.VData.Addr

  let getLayer (v: Vertex<VNode>) =
    v.VData.Layer

  let setLayer (v: Vertex<VNode>) layer = v.VData.Layer <- layer

  let getIsDummy (v: Vertex<VNode>) =
    v.VData.IsDummy

  let getPos (v: Vertex<VNode>) =
    v.VData.Pos

  let getXPos (v: Vertex<VNode>) =
    v.VData.XPos

  let getYPos (v: Vertex<VNode>) =
    v.VData.YPos

  let getWidth (v: Vertex<VNode>) =
    v.VData.Width

  let getHeight (v: Vertex<VNode>) =
    v.VData.Height

