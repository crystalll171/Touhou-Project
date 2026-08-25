#!/usr/bin/env ruby
# frozen_string_literal: true

require 'socket'
require 'ipaddr'
require 'openssl'
require 'net/http'
require 'uri'
require 'timeout'
require 'optparse'
require 'json'
require 'csv'
require 'resolv'
require 'time'

# ==============================================================================
# рџЋЁ ANSI Color & Formatting Helper
# ==============================================================================
module Colors
  RESET   = "\e[0m"
  BOLD    = "\e[1m"
  DIM     = "\e[2m"
  RED     = "\e[31m"
  GREEN   = "\e[32m"
  YELLOW  = "\e[33m"
  BLUE    = "\e[34m"
  MAGENTA = "\e[35m"
  CYAN    = "\e[36m"
  WHITE   = "\e[37m"
  GRAY    = "\e[90m"

  def self.green(text);   "#{GREEN}#{text}#{RESET}"; end
  def self.red(text);     "#{RED}#{text}#{RESET}"; end
  def self.yellow(text);  "#{YELLOW}#{text}#{RESET}"; end
  def self.cyan(text);    "#{CYAN}#{text}#{RESET}"; end
  def self.blue(text);    "#{BLUE}#{text}#{RESET}"; end
  def self.magenta(text); "#{MAGENTA}#{text}#{RESET}"; end
  def self.white(text);   "#{WHITE}#{text}#{RESET}"; end
  def self.bold(text);    "#{BOLD}#{text}#{RESET}"; end
  def self.dim(text);     "#{DIM}#{text}#{RESET}"; end
  def self.gray(text);    "#{GRAY}#{text}#{RESET}"; end

  def self.colorize_status(status)
    case status.to_s.upcase
    when 'OPEN'    then green("  [+] OPEN   ")
    when 'CLOSED'  then red("  [-] CLOSED ")
    when 'TIMEOUT' then gray("  [?] TIMEOUT")
    else gray("  [.] #{status.to_s.upcase}")
    end
  end
end

# ==============================================================================
# рџЊђ Target & Subnet Manager (CIDR Expansion, Validation, Sorting)
# ==============================================================================
class TargetManager
  KNOWN_SERVICES = {
    20 => 'FTP-Data', 21 => 'FTP', 22 => 'SSH', 23 => 'Telnet', 25 => 'SMTP',
    53 => 'DNS', 80 => 'HTTP', 110 => 'POP3', 143 => 'IMAP', 443 => 'HTTPS',
    445 => 'SMB', 1433 => 'MSSQL', 1521 => 'Oracle', 3306 => 'MySQL',
    3389 => 'RDP', 5432 => 'PostgreSQL', 5900 => 'VNC', 6379 => 'Redis',
    8000 => 'HTTP-Alt', 8080 => 'HTTP-Proxy', 8443 => 'HTTPS-Alt',
    8888 => 'HTTP-Alt', 9200 => 'Elasticsearch', 27017 => 'MongoDB'
  }.freeze

  PORT_PRESETS = {
    'web'      => [80, 443, 8000, 8080, 8443, 8888],
    'top10'    => [21, 22, 25, 80, 110, 443, 3306, 3389, 5432, 8080],
    'common'   => [21, 22, 23, 25, 53, 80, 110, 143, 443, 445, 1433, 3306, 3389, 5432, 6379, 8080, 8443, 27017],
    'ssh'      => [22, 2222],
    'database' => [1433, 1521, 3306, 5432, 6379, 27017]
  }.freeze

  # Р’Р°Р»РёРґР°С†РёСЏ С„РѕСЂРјР°С‚Р° IP РёР»Рё CIDR
  def self.valid_ip_or_cidr?(str)
    IPAddr.new(str.to_s.strip)
    true
  rescue IPAddr::InvalidAddressError
    false
  end

  # Р Р°Р·Р±РѕСЂ РїРѕСЂС‚РѕРІ РёР· СЃС‚СЂРѕРєРё (РЅР°РїСЂРёРјРµСЂ, "80,443,8000-8010,web")
  def self.parse_ports(ports_input)
    return [80] if ports_input.nil? || ports_input.to_s.strip.empty?

    ports = []
    ports_input.to_s.split(',').map(&:strip).each do |item|
      if PORT_PRESETS.key?(item.downcase)
        ports.concat(PORT_PRESETS[item.downcase])
      elsif item.include?('-')
        range_parts = item.split('-').map(&:strip)
        if range_parts.size == 2
          first, last = range_parts.map(&:to_i)
          ports.concat((first..last).to_a) if first > 0 && last <= 65_535 && first <= last
        end
      elsif item =~ /^\d+$/
        p_int = item.to_i
        ports << p_int if p_int > 0 && p_int <= 65_535
      end
    end
    ports.uniq.sort
  end

  # Р—Р°РіСЂСѓР·РєР° Рё РѕР±СЂР°Р±РѕС‚РєР° С†РµР»РµР№ РёР· С„Р°Р№Р»Р°
  def self.load_from_file(file_path, expand_cidr: true, max_cidr_hosts: 512, overwrite_cleaned: false)
    raise Errno::ENOENT, "Р¤Р°Р№Р» '#{file_path}' РЅРµ РЅР°Р№РґРµРЅ!" unless File.exist?(file_path)

    raw_lines = File.readlines(file_path, chomp: true)
    cleaned_lines = raw_lines.map(&:strip).reject(&:empty?).reject { |l| l.start_with?('#') }
    
    valid_targets = cleaned_lines.select { |line| valid_ip_or_cidr?(line) }

    # РЎРѕСЂС‚РёСЂРѕРІРєР° РёСЃС…РѕРґРЅС‹С… Р°РґСЂРµСЃРѕРІ
    sorted_targets = valid_targets.uniq.sort_by do |target|
      ip_part = target.split('/').first
      IPAddr.new(ip_part)
    end

    if overwrite_cleaned
      File.open(file_path, 'w') do |file|
        sorted_targets.each { |t| file.puts(t) }
      end
    end

    expanded_hosts = []
    sorted_targets.each do |target|
      if target.include?('/')
        if expand_cidr
          ip_obj = IPAddr.new(target)
          range_size = ip_obj.to_range.count
          if range_size <= max_cidr_hosts
            expanded_hosts.concat(ip_obj.to_range.map(&:to_s))
          else
            # Р”Р»СЏ СЃР»РёС€РєРѕРј Р±РѕР»СЊС€РёС… РїРѕРґСЃРµС‚РµР№ Р±РµСЂРµРј РїРµСЂРІС‹Р№ IP
            expanded_hosts << ip_obj.to_range.first.to_s
          end
        else
          # Р•СЃР»Рё СЂР°Р·РІРѕСЂР°С‡РёРІР°РЅРёРµ РѕС‚РєР»СЋС‡РµРЅРѕ, Р±РµСЂРµРј Р±Р°Р·РѕРІС‹Р№ Р°РґСЂРµСЃ СЃРµС‚Рё
          expanded_hosts << target.split('/').first
        end
      else
        expanded_hosts << target
      end
    end

    expanded_hosts.uniq
  end
end

# ==============================================================================
# рџ•µпёЏ Service & Banner Grabber (HTTP, SSL, SSH, Generic)
# ==============================================================================
class BannerGrabber
  # РЎР±РѕСЂ Р±Р°РЅРЅРµСЂР° / Р·Р°РіРѕР»РѕРІРєР° РґР»СЏ РѕС‚РєСЂС‹С‚РѕРіРѕ РїРѕСЂС‚Р°
  def self.grab(ip, port, timeout_sec = 1.2)
    info = {
      banner: nil,
      http_status: nil,
      http_server: nil,
      http_title: nil
    }

    if [80, 443, 8080, 8443, 8000, 8888, 3000, 5000].include?(port)
      grab_http(ip, port, timeout_sec, info)
    else
      grab_generic_socket(ip, port, timeout_sec, info)
    end

    info
  end

  def self.grab_http(ip, port, timeout_sec, info)
    use_ssl = (port == 443 || port == 8443)
    
    http = Net::HTTP.new(ip, port)
    http.open_timeout = timeout_sec
    http.read_timeout = timeout_sec
    
    if use_ssl
      http.use_ssl = true
      http.verify_mode = OpenSSL::SSL::VERIFY_NONE
    end

    req = Net::HTTP::Get.new('/')
    req['User-Agent'] = 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) ServerScanner/2.0'
    req['Accept'] = '*/*'
    req['Connection'] = 'close'

    response = http.request(req)
    info[:http_status] = "#{response.code} #{response.message}"
    info[:http_server] = response['server']

    body = response.body.to_s
    if body =~ /<title[^>]*>(.*?)<\/title>/im
      clean_title = $1.gsub(/\s+/, ' ').strip
      # РћР±СЂРµР·Р°РµРј РґР»РёРЅСѓ Р·Р°РіРѕР»РѕРІРєР° РґР»СЏ РєРѕРјРїР°РєС‚РЅРѕРіРѕ РІС‹РІРѕРґР°
      info[:http_title] = clean_title[0..70]
    end
  rescue => _e
    # Р•СЃР»Рё Net::HTTP СѓРїР°Р» (РЅР°РїСЂРёРјРµСЂ, СЃРµСЂРІРµСЂ РЅРµ HTTP), РїСЂРѕР±СѓРµРј generic СЃРѕРєРµС‚
    grab_generic_socket(ip, port, timeout_sec, info) if info[:http_status].nil?
  ensure
    http.finish rescue nil if http && http.started?
  end

  def self.grab_generic_socket(ip, port, timeout_sec, info)
    Socket.tcp(ip, port, connect_timeout: timeout_sec) do |sock|
      # Р–РґРµРј РґР°РЅРЅС‹Рµ РѕС‚ СЃРµСЂРІРёСЃР° СЃ РєРѕСЂРѕС‚РєРёРј С‚Р°Р№РјР°СѓС‚РѕРј
      if IO.select([sock], nil, nil, timeout_sec)
        banner_line = sock.gets
        if banner_line && !banner_line.strip.empty?
          clean_b = banner_line.strip.gsub(/[^[:print:]]/, '')
          info[:banner] = clean_b[0..80]
        end
      end
    end
  rescue => _e
    # РРіРЅРѕСЂРёСЂСѓРµРј СЃРµС‚РµРІС‹Рµ РѕС€РёР±РєРё
  end

  # Reverse DNS Lookup
  def self.resolve_hostname(ip)
    Resolv.getname(ip)
  rescue Resolv::ResolvError, Resolv::ResolvTimeout, StandardError
    nil
  end
end

# ==============================================================================
# рџ“Љ Progress Bar & Console UI Formatter
# ==============================================================================
class ConsoleUI
  def initialize(total_tasks, verbose: false)
    @total_tasks = total_tasks
    @processed   = 0
    @open_count  = 0
    @start_time  = Time.now
    @mutex       = Mutex.new
    @verbose     = verbose
  end

  def print_banner(options, targets_count, ports)
    puts Colors.cyan(<<~BANNER)
      в•”в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•—
      в•‘              вљЎ ADVANCED RUBY NETWORK & PORT SCANNER вљЎ                      в•‘
      в•љв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ќ
    BANNER
    puts "  #{Colors.bold('Р¦РµР»РµР№ Рє СЃРєР°РЅРёСЂРѕРІР°РЅРёСЋ:')} #{Colors.green(targets_count)} С…РѕСЃС‚РѕРІ"
    puts "  #{Colors.bold('РџРѕСЂС‚С‹:')}                #{Colors.yellow(ports.join(', '))} (#{ports.size} РїРѕСЂС‚РѕРІ)"
    puts "  #{Colors.bold('РџРѕС‚РѕРєРѕРІ (Workers):')}    #{Colors.cyan(options[:threads])}"
    puts "  #{Colors.bold('РўР°Р№РјР°СѓС‚ РїРѕРґРєР»СЋС‡РµРЅРёСЏ:')} #{options[:timeout]} СЃРµРє"
    puts "  #{Colors.bold('Banner Grabbing:')}      #{options[:banners] ? Colors.green('Р’РєР»СЋС‡РµРЅ') : Colors.gray('Р’С‹РєР»СЋС‡РµРЅ')}"
    puts "  #{Colors.bold('Reverse DNS:')}          #{options[:rdns] ? Colors.green('Р’РєР»СЋС‡РµРЅ') : Colors.gray('Р’С‹РєР»СЋС‡РµРЅ')}"
    puts "  #{Colors.bold('Р¤Р°Р№Р» СЂРµР·СѓР»СЊС‚Р°С‚РѕРІ:')}     #{options[:output] ? Colors.magenta(options[:output]) : Colors.gray('РќРµ СѓРєР°Р·Р°РЅ')}"
    puts "в•ђ" * 79
    puts ""
  end

  def log_open_port(result)
    @mutex.synchronize do
      @open_count += 1
      clear_progress_line

      service_name = TargetManager::KNOWN_SERVICES[result[:port]] || 'Unknown'
      line = "#{Colors.colorize_status('OPEN')} #{Colors.bold(result[:ip].ljust(15))}:#{Colors.yellow(result[:port].to_s.ljust(5))} (#{Colors.cyan(service_name.ljust(9))})"
      
      details = []
      details << "#{Colors.gray(result[:latency_ms].to_s + 'ms')}" if result[:latency_ms]
      details << "Host: #{Colors.blue(result[:hostname])}" if result[:hostname]
      details << "HTTP: #{Colors.green(result[:http_status])}" if result[:http_status]
      details << "Server: #{Colors.magenta(result[:http_server])}" if result[:http_server]
      details << "Title: #{Colors.white('"' + result[:http_title] + '"')}" if result[:http_title]
      details << "Banner: #{Colors.gray(result[:banner])}" if result[:banner]

      line += " [#{details.join(' | ')}]" unless details.empty?
      puts line
    end
  end

  def log_closed_port(ip, port, reason)
    return unless @verbose

    @mutex.synchronize do
      clear_progress_line
      puts "#{Colors.colorize_status('CLOSED')} #{ip.ljust(15)}:#{port.to_s.ljust(5)} (#{Colors.gray(reason)})"
    end
  end

  def increment_and_render_progress
    @mutex.synchronize do
      @processed += 1
      render_progress_bar
    end
  end

  def clear_progress_line
    print "\r\e[K"
  end

  def render_progress_bar
    elapsed = [Time.now - @start_time, 0.001].max
    rate = (@processed / elapsed).round(1)
    percent = @total_tasks > 0 ? ((@processed.to_f / @total_tasks) * 100).round(1) : 100.0

    bar_length = 24
    filled = ((percent / 100.0) * bar_length).to_i
    bar = "в– " * filled + "В·" * (bar_length - filled)

    eta_sec = rate > 0 ? [((@total_tasks - @processed) / rate).to_i, 0].max : 0
    eta_str = "#{eta_sec}s"

    print "\r#{Colors.gray('[')}#{Colors.cyan(bar)}#{Colors.gray(']')} #{Colors.bold("#{percent}%")} " \
          "(#{@processed}/#{@total_tasks}) | " \
          "#{Colors.green("РћС‚РєСЂС‹С‚Рѕ: #{@open_count}")} | " \
          "#{rate} t/s | ETA: #{eta_str} \e[K"
  end

  def print_summary(total_time, open_results)
    clear_progress_line
    puts "\n" + "в•ђ" * 79
    puts Colors.bold("рџ“Љ РРўРћР“РћР’Р«Р™ РћРўР§Р•Рў РЎРљРђРќРР РћР’РђРќРРЇ:")
    puts "  Р’СЃРµРіРѕ РїСЂРѕРІРµСЂРµРЅРѕ Р·Р°РґР°С‡:   #{Colors.bold(@processed)}"
    puts "  РќР°Р№РґРµРЅРѕ РѕС‚РєСЂС‹С‚С‹С… РїРѕСЂС‚РѕРІ: #{Colors.green(open_results.size)}"
    puts "  РћР±С‰РµРµ РІСЂРµРјСЏ:             #{Colors.cyan(total_time.round(2).to_s + ' СЃРµРє')}"
    speed = (@processed / [total_time, 0.001].max).round(1)
    puts "  РЎСЂРµРґРЅСЏСЏ СЃРєРѕСЂРѕСЃС‚СЊ:        #{Colors.yellow(speed.to_s + ' Р·Р°РґР°С‡/СЃРµРє')}"
    puts "в•ђ" * 79
  end
end

# ==============================================================================
# рџ’ѕ Results Exporter (JSON, CSV, TXT)
# ==============================================================================
class ResultExporter
  def self.export(results, file_path)
    return if file_path.nil? || file_path.strip.empty?

    ext = File.extname(file_path).downcase
    case ext
    when '.json'
      export_json(results, file_path)
    when '.csv'
      export_csv(results, file_path)
    when '.xls', '.xlsx'
      export_excel(results, file_path)
    else
      export_txt(results, file_path)
    end
    puts "рџ’ѕ Р РµР·СѓР»СЊС‚Р°С‚С‹ СѓСЃРїРµС€РЅРѕ СЃРѕС…СЂР°РЅРµРЅС‹ РІ: #{Colors.magenta(file_path)}"
  end

  def self.export_json(results, file_path)
    File.write(file_path, JSON.pretty_generate(results))
  end

  def self.export_csv(results, file_path)
    CSV.open(file_path, 'wb', col_sep: ',') do |csv|
      csv << %w[ip hostname port service status latency_ms http_status http_server http_title banner]
      results.each do |r|
        csv << [
          r[:ip],
          r[:hostname],
          r[:port],
          TargetManager::KNOWN_SERVICES[r[:port]] || 'Unknown',
          r[:status],
          r[:latency_ms],
          r[:http_status],
          r[:http_server],
          r[:http_title],
          r[:banner]
        ]
      end
    end
  end

  def self.export_txt(results, file_path)
    File.open(file_path, 'w') do |f|
      f.puts "=== РћРўР§Р•Рў РЎРљРђРќРР РћР’РђРќРРЇ РЎР•Р Р’Р•Р РћР’ (#{Time.now}) ==="
      results.each do |r|
        service = TargetManager::KNOWN_SERVICES[r[:port]] || 'Unknown'
        f.puts "#{r[:ip]}:#{r[:port]} [#{service}] - #{r[:latency_ms]}ms"
        f.puts "  Hostname: #{r[:hostname]}" if r[:hostname]
        f.puts "  HTTP: #{r[:http_status]} | Server: #{r[:http_server]} | Title: #{r[:http_title]}" if r[:http_title] || r[:http_server]
        f.puts "  Banner: #{r[:banner]}" if r[:banner]
        f.puts "-" * 40
      end
    end
  end

  def self.export_excel(results, file_path)
    File.open(file_path, 'w', encoding: 'UTF-8') do |f|
      f.puts "<html><head><meta charset='utf-8'></head><body><table>"
      f.puts "<tr><th style='background-color:#ff3377;color:white;'>IP</th><th style='background-color:#ff3377;color:white;'>Port</th><th style='background-color:#ff3377;color:white;'>Service</th><th style='background-color:#ff3377;color:white;'>Status</th><th style='background-color:#ff3377;color:white;'>LatencyMs</th><th style='background-color:#ff3377;color:white;'>Hostname</th><th style='background-color:#ff3377;color:white;'>HttpStatus</th><th style='background-color:#ff3377;color:white;'>HttpServer</th><th style='background-color:#ff3377;color:white;'>HttpTitle</th></tr>"
      results.each do |r|
        f.puts "<tr><td>#{r[:ip]}</td><td>#{r[:port]}</td><td>#{TargetManager::KNOWN_SERVICES[r[:port]] || 'Unknown'}</td><td>OPEN</td><td>#{r[:latency_ms]}</td><td>#{r[:hostname]}</td><td>#{r[:http_status]}</td><td>#{r[:http_server]}</td><td>#{r[:http_title]}</td></tr>"
      end
      f.puts "</table></body></html>"
    end
  end
end

# ==============================================================================
# рџљЂ Core Server Scanner Engine
# ==============================================================================
class ServerScanner
  attr_accessor :options, :results

  def initialize(options = {})
    @options = {
      targets_file: 'targets.txt',
      single_target: nil,
      ports_raw: '80,443',
      threads: 40,
      timeout: 1.0,
      output: nil,
      banners: true,
      rdns: false,
      expand_cidr: false,
      max_cidr_hosts: 256,
      verbose: false,
      clean_file: false
    }.merge(options)

    @results = []
    @mutex   = Mutex.new
  end

  def run
    # 1. Р—Р°РіСЂСѓР·РєР° С†РµР»РµР№
    targets = []
    if @options[:single_target]
      if @options[:single_target].include?('/') && @options[:expand_cidr]
        ip_obj = IPAddr.new(@options[:single_target])
        targets = ip_obj.to_range.map(&:to_s)
      else
        targets = [@options[:single_target]]
      end
    elsif @options[:targets_file] && File.exist?(@options[:targets_file])
      targets = TargetManager.load_from_file(
        @options[:targets_file],
        expand_cidr: @options[:expand_cidr],
        max_cidr_hosts: @options[:max_cidr_hosts],
        overwrite_cleaned: @options[:clean_file]
      )
    else
      puts Colors.red("РћС€РёР±РєР°: РЅРµ СѓРєР°Р·Р°РЅС‹ С†РµР»Рё РґР»СЏ СЃРєР°РЅРёСЂРѕРІР°РЅРёСЏ (С„Р°Р№Р» РЅРµ РЅР°Р№РґРµРЅ РёР»Рё РЅРµ РїРµСЂРµРґР°РЅ -i).")
      return
    end

    if targets.empty?
      puts Colors.yellow("РџСЂРµРґСѓРїСЂРµР¶РґРµРЅРёРµ: СЃРїРёСЃРѕРє С†РµР»РµР№ РїСѓСЃС‚.")
      return
    end

    # 2. РџРѕРґРіРѕС‚РѕРІРєР° РїРѕСЂС‚РѕРІ
    ports = TargetManager.parse_ports(@options[:ports_raw])
    if ports.empty?
      puts Colors.red("РћС€РёР±РєР°: РЅРµ СѓРєР°Р·Р°РЅС‹ РІР°Р»РёРґРЅС‹Рµ РїРѕСЂС‚С‹ РґР»СЏ СЃРєР°РЅРёСЂРѕРІР°РЅРёСЏ.")
      return
    end

    # 3. Р¤РѕСЂРјРёСЂРѕРІР°РЅРёРµ РѕС‡РµСЂРµРґРё Р·Р°РґР°С‡ [ip, port]
    work_queue = Queue.new
    targets.each do |ip|
      ports.each do |port|
        work_queue << [ip, port]
      end
    end

    total_tasks = work_queue.size
    ui = ConsoleUI.new(total_tasks, verbose: @options[:verbose])
    ui.print_banner(@options, targets.size, ports)

    start_time = Time.now

    # 4. Р—Р°РїСѓСЃРє РїСѓР»Р° РїРѕС‚РѕРєРѕРІ
    thread_count = [@options[:threads], total_tasks].min
    thread_count = [thread_count, 1].max

    workers = thread_count.times.map do
      Thread.new do
        while !work_queue.empty?
          task = nil
          begin
            task = work_queue.pop(true)
          rescue ThreadError
            break
          end

          break if task.nil?
          ip, port = task

          begin
            scan_target(ip, port, ui)
          rescue => e
            ui.log_closed_port(ip, port, "Error: #{e.message}")
          ensure
            ui.increment_and_render_progress
          end
        end
      end
    end

    # РћР¶РёРґР°РЅРёРµ Р·Р°РІРµСЂС€РµРЅРёСЏ РІСЃРµС… РїРѕС‚РѕРєРѕРІ
    workers.each(&:join)
    total_time = Time.now - start_time

    # 5. РС‚РѕРіРѕРІС‹Р№ РѕС‚С‡РµС‚ Рё СЌРєСЃРїРѕСЂС‚
    ui.print_summary(total_time, @results)
    ResultExporter.export(@results, @options[:output]) if @options[:output]
  end

  private

  def scan_target(ip, port, ui)
    t_start = Process.clock_gettime(Process::CLOCK_MONOTONIC)
    is_open = false

    begin
      Socket.tcp(ip, port, connect_timeout: @options[:timeout]) do |_sock|
        is_open = true
      end
    rescue Errno::ECONNREFUSED
      ui.log_closed_port(ip, port, 'Connection Refused')
    rescue Errno::EHOSTUNREACH, Errno::ENETUNREACH
      ui.log_closed_port(ip, port, 'Host Unreachable')
    rescue Errno::ETIMEDOUT, SocketError
      ui.log_closed_port(ip, port, 'Timeout')
    rescue => e
      ui.log_closed_port(ip, port, e.class.to_s)
    end

    return unless is_open

    t_end = Process.clock_gettime(Process::CLOCK_MONOTONIC)
    latency = ((t_end - t_start) * 1000).round(1)

    # Р”РѕРїРѕР»РЅРёС‚РµР»СЊРЅР°СЏ СЂР°Р·РІРµРґРєР°
    hostname = @options[:rdns] ? BannerGrabber.resolve_hostname(ip) : nil
    grab_info = @options[:banners] ? BannerGrabber.grab(ip, port, @options[:timeout] * 1.5) : {}

    result_item = {
      ip: ip,
      hostname: hostname,
      port: port,
      status: 'OPEN',
      latency_ms: latency,
      http_status: grab_info[:http_status],
      http_server: grab_info[:http_server],
      http_title: grab_info[:http_title],
      banner: grab_info[:banner]
    }

    @mutex.synchronize do
      @results << result_item
    end

    ui.log_open_port(result_item)
  end
end

# ==============================================================================
# рџЋ›пёЏ CLI Options Parser & Entry Point
def parse_arguments
  default_targets = if File.exist?('data/targets.txt')
                      'data/targets.txt'
                    elsif File.exist?('targets.txt')
                      'targets.txt'
                    else
                      nil
                    end

  options = {
    targets_file: default_targets,
    single_target: nil,
    ports_raw: '80,443',
    threads: 40,
    timeout: 1.0,
    output: nil,
    banners: true,
    rdns: false,
    expand_cidr: false,
    max_cidr_hosts: 256,
    verbose: false,
    clean_file: false
  }

  parser = OptionParser.new do |opts|
    opts.banner = "РСЃРїРѕР»СЊР·РѕРІР°РЅРёРµ: ruby Ruby.rb [РѕРїС†РёРё]"
    opts.separator ""
    opts.separator "РћСЃРЅРѕРІРЅС‹Рµ РѕРїС†РёРё:"

    opts.on("-f", "--file FILE", "Р¤Р°Р№Р» СЃРѕ СЃРїРёСЃРєРѕРј IP-Р°РґСЂРµСЃРѕРІ / CIDR (РїРѕ СѓРјРѕР»С‡Р°РЅРёСЋ: targets.txt)") do |f|
      options[:targets_file] = f
    end

    opts.on("-i", "--ip TARGET", "РћРґРёРЅРѕС‡РЅС‹Р№ IP Р°РґСЂРµСЃ РёР»Рё CIDR (РЅР°РїСЂРёРјРµСЂ, 192.168.1.1 РёР»Рё 10.0.0.0/24)") do |i|
      options[:single_target] = i
    end

    opts.on("-p", "--ports PORTS", "РџРѕСЂС‚С‹ РґР»СЏ СЃРєР°РЅРёСЂРѕРІР°РЅРёСЏ: '80,443', '1-1024', РїСЂРµСЃРµС‚С‹: web, top10, common, ssh, database") do |p|
      options[:ports_raw] = p
    end

    opts.on("-t", "--threads NUM", Integer, "РљРѕР»РёС‡РµСЃС‚РІРѕ СЂР°Р±РѕС‡РёС… РїРѕС‚РѕРєРѕРІ (РїРѕ СѓРјРѕР»С‡Р°РЅРёСЋ: 40)") do |t|
      options[:threads] = t
    end

    opts.on("-T", "--timeout SEC", Float, "РўР°Р№РјР°СѓС‚ РїРѕРґРєР»СЋС‡РµРЅРёСЏ РІ СЃРµРєСѓРЅРґР°С… (РїРѕ СѓРјРѕР»С‡Р°РЅРёСЋ: 1.0)") do |to|
      options[:timeout] = to
    end

    opts.on("-o", "--output FILE", "Файл для сохранения отчета (.json, .csv, .txt, .xls)") do |o|
      options[:output] = o
    end

    opts.separator ""
    opts.separator "РћРїС†РёРё СЂР°Р·РІРµРґРєРё Рё СЂРµР¶РёРјР° СЂР°Р±РѕС‚С‹:"

    opts.on("-b", "--[no-]banners", "Р—Р°С…РІР°С‚С‹РІР°С‚СЊ HTTP Р·Р°РіРѕР»РѕРІРєРё, Title Рё Р±Р°РЅРЅРµСЂС‹ СЃРµСЂРІРёСЃРѕРІ (РІРєР»СЋС‡РµРЅРѕ РїРѕ СѓРјРѕР»С‡Р°РЅРёСЋ)") do |b|
      options[:banners] = b
    end

    opts.on("-r", "--[no-]rdns", "Р’С‹РїРѕР»РЅСЏС‚СЊ Reverse DNS (РѕРїСЂРµРґРµР»РµРЅРёРµ РґРѕРјРµРЅР° С…РѕСЃС‚Р°)") do |r|
      options[:rdns] = r
    end

    opts.on("-e", "--[no-]expand-cidr", "Р Р°Р·РІРѕСЂР°С‡РёРІР°С‚СЊ CIDR РїРѕРґСЃРµС‚Рё (РґРѕ /24) РІ СЃРїРёСЃРѕРє С…РѕСЃС‚РѕРІ (РїРѕ СѓРјРѕР»С‡Р°РЅРёСЋ: РІС‹РєР»)") do |e|
      options[:expand_cidr] = e
    end

    opts.on("--clean-file", "РћС‡РёСЃС‚РёС‚СЊ, РѕС‚СЃРѕСЂС‚РёСЂРѕРІР°С‚СЊ Рё РїРµСЂРµР·Р°РїРёСЃР°С‚СЊ РёСЃС…РѕРґРЅС‹Р№ С„Р°Р№Р» С†РµР»РµР№") do
      options[:clean_file] = true
    end

    opts.on("-v", "--verbose", "РџРѕРґСЂРѕР±РЅС‹Р№ РІС‹РІРѕРґ (РѕС‚РѕР±СЂР°Р¶Р°С‚СЊ Р·Р°РєСЂС‹С‚С‹Рµ РїРѕСЂС‚С‹)") do
      options[:verbose] = true
    end

    opts.on("-h", "--help", "РџРѕРєР°Р·Р°С‚СЊ СЌС‚Сѓ СЃРїСЂР°РІРєСѓ") do
      puts opts
      exit
    end
  end

  parser.parse!(ARGV)
  options
end

# Р—Р°РїСѓСЃРє СЃРєСЂРёРїС‚Р°
if __FILE__ == $PROGRAM_NAME
  opts = parse_arguments
  scanner = ServerScanner.new(opts)
  scanner.run
end
