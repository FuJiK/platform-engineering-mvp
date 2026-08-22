(ns platform-mvp.mcp
  (:require [clojure.data.json :as json]
            [clojure.java.io :as io]
            [clojure.string :as str]
            [platform-mvp.docker :as docker]
            [platform-mvp.policy :as policy]))

;; MCP protocol version negotiated during initialize.
;; This matches the MCP specification stable release (not a placeholder):
;; https://modelcontextprotocol.io/specification/2025-11-25
(def protocol-version "2025-11-25")
(def policy-path "generated/ops-policy.json")

(defn log!
  [& xs]
  (binding [*out* *err*]
    (apply println xs)))

(defn read-policy
  []
  (when-not (.exists (io/file policy-path))
    (throw (ex-info "generated/ops-policy.json does not exist. Run `clojure -M:generate` first."
                    {:path policy-path})))
  (json/read-str (slurp policy-path) :key-fn keyword))

(defn current-role
  []
  (keyword (or (System/getenv "PLATFORM_ROLE") "operator")))

(defn json-rpc-result
  [id result]
  {:jsonrpc "2.0"
   :id id
   :result result})

(defn json-rpc-error
  ([id code message]
   (json-rpc-error id code message nil))
  ([id code message data]
   {:jsonrpc "2.0"
    :id id
    :error (cond-> {:code code
                    :message message}
             data (assoc :data data))}))

(defn text-result
  [text]
  {:content [{:type "text"
              :text (str text)}]
   :isError false})

(defn tool-error
  [message]
  {:content [{:type "text"
              :text message}]
   :isError true})

(def tool-definitions
  {:get-status
   {:name "get_status"
    :description "Get the current Docker container status for the managed service. Read-only."
    :inputSchema {:type "object"
                  :additionalProperties false}}

   :get-logs
   {:name "get_logs"
    :description "Read recent logs from the managed Docker container. Read-only."
    :inputSchema {:type "object"
                  :properties {:lines {:type "integer"
                                       :minimum 1
                                       :maximum 200}}
                  :additionalProperties false}}

   :restart-service
   {:name "restart_service"
    :description "Restart the managed Docker container. This mutation is policy-gated and requires an explicit approved=true argument."
    :inputSchema {:type "object"
                  :properties {:approved {:type "boolean"}}
                  :required ["approved"]
                  :additionalProperties false}}})

(def tool-name->operation
  (into {}
        (map (fn [[operation definition]]
               [(:name definition) operation]))
        tool-definitions))

(defn visible-tools
  [ops-policy role]
  (->> tool-definitions
       (keep (fn [[operation definition]]
               (when (policy/role-allowed? ops-policy role operation)
                 definition)))
       vec))

(defn ensure-authorized!
  [ops-policy role operation]
  (when-not (policy/role-allowed? ops-policy role operation)
    (throw (ex-info "Operation is not allowed for this role."
                    {:role (name role)
                     :operation (name operation)}))))

(defn normalize-lines
  [ops-policy arguments]
  (let [requested (or (:lines arguments) 50)
        policy-max (or (get-in ops-policy [:operations :get-logs :maxLines]) 200)]
    (when-not (and (integer? requested) (<= 1 requested policy-max))
      (throw (ex-info "lines is outside the permitted range."
                      {:requested requested
                       :minimum 1
                       :maximum policy-max})))
    requested))

(defn call-tool
  [ops-policy role tool-name arguments]
  (let [operation (get tool-name->operation tool-name)
        container-name (:service ops-policy)]
    (when-not operation
      (throw (ex-info "Unknown tool."
                      {:tool tool-name})))
    (ensure-authorized! ops-policy role operation)

    (case operation
      :get-status
      (text-result
       (str "service=" container-name
            " status=" (docker/get-status container-name)))

      :get-logs
      (let [lines (normalize-lines ops-policy arguments)]
        (text-result (docker/get-logs container-name lines)))

      :restart-service
      (do
        (when-not (= true (:approved arguments))
          (throw (ex-info "restart_service requires approved=true."
                          {:approved (:approved arguments)})))
        (text-result
         (str "restarted=" (docker/restart-service container-name)))))))

(defn initialize-result
  [requested-version]
  {:protocolVersion (if (= protocol-version requested-version)
                      requested-version
                      protocol-version)
   :capabilities {:tools {:listChanged false}}
   :serverInfo {:name "platform-engineering-mvp"
                :title "Platform Engineering MVP"
                :version "0.1.0"
                :description "Policy-gated Day-2 operations over a locally provisioned Docker service."}
   :instructions "This server exposes only policy-approved operations. Destructive delete/destroy tools do not exist."})

(defn handle-request
  [ops-policy role request]
  (let [id (:id request)
        method (:method request)
        params (:params request)]
    (try
      (case method
        "initialize"
        (json-rpc-result id
                         (initialize-result (:protocolVersion params)))

        "ping"
        (json-rpc-result id {})

        "tools/list"
        (json-rpc-result id {:tools (visible-tools ops-policy role)})

        "tools/call"
        (let [tool-name (:name params)
              arguments (or (:arguments params) {})]
          (if-not (contains? tool-name->operation tool-name)
            (json-rpc-error id -32602 (str "Unknown tool: " tool-name))
            (json-rpc-result id
                             (call-tool ops-policy role tool-name arguments))))

        "notifications/initialized"
        nil

        (if (some? id)
          (json-rpc-error id -32601 (str "Method not found: " method))
          nil))
      (catch clojure.lang.ExceptionInfo e
        (if (some? id)
          (json-rpc-result id
                           (tool-error
                            (str (.getMessage e)
                                 " data=" (pr-str (ex-data e)))))
          nil))
      (catch Exception e
        (log! "Unhandled error:" (.getMessage e))
        (if (some? id)
          (json-rpc-error id -32603 "Internal error")
          nil)))))

(defn emit!
  [message]
  (when message
    ;; stdout is reserved exclusively for MCP JSON-RPC messages.
    (println (json/write-str message :escape-slash false))
    (flush)))

(defn process-line!
  [ops-policy role line]
  (when-not (str/blank? line)
    (try
      (let [request (json/read-str line :key-fn keyword)]
        (emit! (handle-request ops-policy role request)))
      (catch Exception e
        (log! "Invalid JSON-RPC input:" (.getMessage e))
        (emit! (json-rpc-error nil -32700 "Parse error"))))))

(defn -main
  [& _]
  (let [ops-policy (read-policy)
        role (current-role)]
    (log! "Starting Platform Engineering MVP MCP server"
          "role=" (name role)
          "protocol=" protocol-version)
    (doseq [line (line-seq (java.io.BufferedReader. *in*))]
      (process-line! ops-policy role line))))
