#!/usr/bin/env ruby
# Render Mono's call report as a flamegraph of attributed self time.

ROOT = ARGV[3] || 'NpcTurnBenchmarks:RunTurns ('
TITLE = (ARGV[4] || 'NPC turn: managed call flamegraph').dup.force_encoding('UTF-8')
DETAIL = (ARGV[5] || '8 turns · 1800 actions · width = attributed self time; hover a block for its call path').dup.force_encoding('UTF-8')
METHOD = /^\s*(\d+)\s+(\d+)\s+(\d+)\s+(.+)$/
CALLS = /^\t(\d+) calls from:$/

def escape(value)
  value.gsub('&', '&amp;').gsub('<', '&lt;').gsub('>', '&gt;').gsub('"', '&quot;')
end

def name_short(name)
  name.split(' (', 2).first.gsub('djack.RogueSurvivor.', '')
end

def color(name)
  return '#394352' if name.include?('RunTurns')
  return '#77b58b' if %w[GetZonesAt Zone: Rectangle:Contains].any? { |part| name.include?(part) }
  return '#e7a965' if %w[LOS: LOSSensor: FOV].any? { |part| name.include?(part) }
  return '#7d9fca' if ['.AI.', 'NpcIntent', 'NpcKnowledge'].any? { |part| name.include?(part) }

  '#afb6be'
end

def add(tree, stack, weight)
  node = tree
  stack.each do |method|
    node = (node[:children][method] ||= { name: method, self: 0.0, total: 0.0, children: {} })
    node[:total] += weight
  end
  node[:self] += weight
end

def main(report_path, svg_path, folded_path)
  tree = { children: {} }
  folded = Hash.new(0.0)
  root_name = nil
  root_total = 0
  current = nil
  traces = []
  trace_count = nil
  trace_path = []

  finish_trace = lambda do
    traces << [trace_count, trace_path] unless trace_count.nil?
    trace_count = nil
    trace_path = []
  end
  finish_method = lambda do
    finish_trace.call
    unless current.nil?
      total, self_ms, _count, method = current
      if method.start_with?(ROOT)
        root_name, root_total = method, total
        if self_ms.positive?
          add(tree, [method], self_ms)
          folded[[method]] += self_ms
        end
      elsif self_ms.positive? && !traces.empty?
        all_calls = traces.sum(&:first)
        traces.each do |count, path|
          start = path.index { |frame| frame.start_with?(ROOT) }
          next if start.nil?

          stack = path[start..] + [method]
          weight = self_ms.to_f * count / all_calls
          add(tree, stack, weight)
          folded[stack] += weight
        end
      end
    end
    current = nil
    traces = []
  end

  File.foreach(report_path, encoding: 'UTF-8', invalid: :replace, undef: :replace) do |line|
    match = METHOD.match(line)
    if match && !line.start_with?("\t")
      finish_method.call
      current = [match[1].to_i, match[2].to_i, match[3].to_i, match[4].strip]
    elsif !current.nil?
      match = CALLS.match(line)
      if match
        finish_trace.call
        trace_count = match[1].to_i
      elsif !trace_count.nil? && line.start_with?("\t\t")
        trace_path << line.strip
      end
    end
  end
  finish_method.call

  abort "#{ROOT} was not found in Mono call report" if root_name.nil? || !tree[:children].key?(root_name)

  root = tree[:children][root_name]
  missing = root_total - root[:total]
  if missing > 1
    label = 'Other / native / rounded'
    add(root, [label], missing)
    root[:total] += missing
    folded[[root_name, label]] += missing
  end

  File.open(folded_path, 'w:UTF-8') do |output|
    folded.sort.each do |stack, ms|
      output.puts "#{stack.map { |frame| name_short(frame) }.join(';')} #{(ms * 1000).round(0, half: :even)}" if ms.positive?
    end
  end

  width, left, top, row = 1600, 24, 108, 22
  plot_width = width - 2 * left
  scale = plot_width.to_f / root[:total]
  depth = lambda do |node|
    visible = node[:children].values.select { |child| child[:total] * scale >= 2 }
    1 + (visible.map { |child| depth.call(child) }.max || 0)
  end
  max_depth = depth.call(root)
  height = top + max_depth * row + 45
  shapes = []

  draw = lambda do |node, x, level, lineage|
    w = node[:total] * scale
    y = top + (max_depth - level - 1) * row
    label = name_short(node[:name])
    title = (lineage + [node[:name]]).map { |part| name_short(part) }.join(' → ')
    title += format(' | inclusive ≈ %.1f ms, self ≈ %.1f ms (under Mono profiler)', node[:total], node[:self])
    shapes << format('<g><title>%s</title><rect x="%.2f" y="%d" width="%.2f" height="21" fill="%s" stroke="#fff" stroke-width="0.6"/>',
                     escape(title), x, y, [w, 0.1].max, color(node[:name]))
    if w >= 45
      chars = [(w - 10) / 7.2, 0].max.to_i
      shown = label.length <= chars ? label : label[0, [chars - 1, 0].max] + '…'
      shapes << format('<text x="%.2f" y="%d" font-size="12" font-family="monospace" fill="#17202a">%s</text>',
                       x + 5, y + 15, escape(shown))
    end
    shapes << '</g>'
    child_x = x
    small = 0.0
    node[:children].values.sort_by { |child| -child[:total] }.each do |child|
      if child[:total] * scale < 2
        small += child[:total]
        next
      end
      draw.call(child, child_x, level + 1, lineage + [node[:name]])
      child_x += child[:total] * scale
    end
    if small * scale >= 2
      draw.call({ name: 'Other small calls', self: small, total: small, children: {} },
                child_x, level + 1, lineage + [node[:name]])
    end
  end

  draw.call(root, left, 0, [])
  File.open(svg_path, 'w:UTF-8') do |output|
    output.puts format('<svg xmlns="http://www.w3.org/2000/svg" width="%d" height="%d" viewBox="0 0 %d %d">',
                       width, height, width, height)
    output.puts '<rect width="100%" height="100%" fill="#fff"/>'
    output.puts '<text x="24" y="35" font-size="23" font-family="sans-serif" font-weight="600" fill="#17202a">' + escape(TITLE) + '</text>'
    output.puts '<text x="24" y="59" font-size="13" font-family="sans-serif" fill="#475569">' + escape(DETAIL) + '</text>'
    output.puts '<text x="24" y="79" font-size="12" font-family="sans-serif" fill="#475569">Mono method instrumentation greatly slows execution; time split across multiple callers is estimated from call counts.</text>'
    output.puts shapes.join("\n")
    output.puts format('<text x="24" y="%d" font-size="12" font-family="sans-serif" fill="#475569">Orange: FOV · green: zones · blue: AI · gray: other · Mono total: %d ms · attributed: %.1f ms</text>',
                       height - 18, root_total, root[:total])
    output.puts '</svg>'
  end
end

abort 'usage: npc_flamegraph.rb calls.txt flamegraph.svg flamegraph.folded [root title detail]' unless (3..6).include?(ARGV.length)
main(*ARGV.take(3))
